using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using AmentumRevit.Services;
using AmentumRevit.Models;
using Microsoft.Win32;

namespace AmentumRevit.Commands;

[Transaction(TransactionMode.Manual)]
public class ImportUpdatesFromExcelCommand : IExternalCommand
{
    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        var uidoc = commandData.Application.ActiveUIDocument;
        var doc   = uidoc.Document;

        var dlg = new OpenFileDialog
        {
            Title  = "Amentum — Import Parameter Updates",
            Filter = "Excel Workbook (*.xlsx)|*.xlsx",
        };

        if (dlg.ShowDialog() != true)
            return Result.Cancelled;

        List<ExcelParameterUpdate> updates;
        try
        {
            updates = ExcelService.ReadUpdates(dlg.FileName);
        }
        catch (Exception ex)
        {
            TaskDialog.Show("Amentum — Import Error", $"Failed to read workbook:\n{ex.Message}");
            return Result.Failed;
        }

        if (updates.Count == 0)
        {
            TaskDialog.Show("Amentum — Import",
                "No updates found in the workbook.\n" +
                "Fill in the 'Updates' sheet with UniqueId, ParamKey, and Value columns.");
            return Result.Cancelled;
        }

        // Build a lookup from UniqueId → Element
        var allElements = new FilteredElementCollector(doc)
            .WhereElementIsNotElementType()
            .ToElements();

        var byUniqueId = allElements
            .Where(el => el is not null)
            .ToDictionary(el => el.UniqueId, el => el);

        var map     = ParameterMapService.LoadOrDefault();
        int applied = 0;
        int skipped = 0;
        var errors  = new List<string>();

        using var tx = new Transaction(doc, "Amentum — Import Excel Updates");
        tx.Start();

        foreach (var upd in updates)
        {
            if (!byUniqueId.TryGetValue(upd.ElementUniqueId, out var el))
            {
                errors.Add($"UniqueId not found: {upd.ElementUniqueId}");
                skipped++;
                continue;
            }

            // Resolve spec: prefer Guid override, then key lookup
            ParameterSpec spec;
            if (!string.IsNullOrWhiteSpace(upd.ParameterGuid) &&
                Guid.TryParse(upd.ParameterGuid, out _))
            {
                spec = new ParameterSpec { Guid = upd.ParameterGuid, RevitName = upd.ParameterKey };
            }
            else
            {
                spec = ParameterMapService.Resolve(map, upd.ParameterKey ?? string.Empty);
                if (string.IsNullOrWhiteSpace(spec.Guid) && string.IsNullOrWhiteSpace(spec.RevitName))
                    spec = new ParameterSpec { RevitName = upd.ParameterKey };
            }

            bool ok = RevitParameterUtil.TrySetParameterFromString(el, spec, upd.Value, out string? err);
            if (ok)
                applied++;
            else
            {
                errors.Add($"[{upd.ElementUniqueId}] {upd.ParameterKey}: {err}");
                skipped++;
            }
        }

        tx.Commit();

        string summary = $"Applied: {applied}   Skipped: {skipped}";
        if (errors.Count > 0)
        {
            int shown = Math.Min(errors.Count, 10);
            string tail = errors.Count > 10 ? $"\n… and {errors.Count - 10} more." : string.Empty;
            summary += $"\n\nFirst {shown} error(s):\n" + string.Join("\n", errors.Take(10)) + tail;
        }

        TaskDialog.Show("Amentum — Import Complete", summary);
        return Result.Succeeded;
    }
}
