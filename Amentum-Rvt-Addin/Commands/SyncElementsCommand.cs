using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using AmentumRevit.Models;
using AmentumRevit.Services;
using AmentumRevit.Views;

namespace AmentumRevit.Commands;

/// <summary>
/// Opens the in-process WPF Sync Panel for viewing and editing element parameters.
/// Replaces the original Python-localhost sync approach — no external service required.
///
/// Pattern:
///   1. Collect selected elements and build ElementPayload list (read-only pass).
///   2. Show SyncPanelWindow modally — user edits NewValue cells.
///   3. On Apply, Execute() receives the pending changes and runs a single Transaction.
/// </summary>
[Transaction(TransactionMode.Manual)]
public class SyncElementsCommand : IExternalCommand
{
    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        var uidoc = commandData.Application.ActiveUIDocument;
        var doc   = uidoc.Document;

        var ids = uidoc.Selection.GetElementIds();
        if (ids.Count == 0)
        {
            TaskDialog.Show("Amentum — Sync Panel",
                "No elements selected. Select one or more elements and try again.");
            return Result.Cancelled;
        }

        var map      = ParameterMapService.LoadOrDefault();
        var selected = ids.Select(id => doc.GetElement(id))
                          .Where(el => el is not null)
                          .ToList();

        // Build payloads (read-only — outside any transaction)
        var payloads = selected
            .Select(el => RevitElementPayloadBuilder.Build(
                el, map,
                includeGeometry: true,
                includeElectrical: true))
            .ToList();

        // Show the WPF panel
        var window = new SyncPanelWindow(payloads, map);
        bool? result = window.ShowDialog();

        if (result != true || window.PendingChanges.Count == 0)
            return Result.Cancelled;

        // Build UniqueId → Element lookup
        var byUniqueId = selected.ToDictionary(el => el.UniqueId);

        int applied = 0;
        int skipped = 0;
        var errors  = new List<string>();

        using var tx = new Transaction(doc, "Amentum — Sync Panel Apply");
        tx.Start();

        foreach (var change in window.PendingChanges)
        {
            if (!byUniqueId.TryGetValue(change.UniqueId, out var el))
            {
                errors.Add($"Element not found: {change.UniqueId}");
                skipped++;
                continue;
            }

            var spec = ParameterMapService.Resolve(map, change.ParamKey);
            if (string.IsNullOrWhiteSpace(spec.Guid) && string.IsNullOrWhiteSpace(spec.RevitName))
                spec = new ParameterSpec { RevitName = change.ParamKey };

            bool ok = RevitParameterUtil.TrySetParameterFromString(
                el, spec, change.NewValue, out string? err);

            if (ok) applied++;
            else
            {
                errors.Add($"[{change.UniqueId}] {change.ParamKey}: {err}");
                skipped++;
            }
        }

        tx.Commit();

        string summary = $"Applied: {applied}   Skipped: {skipped}";
        if (errors.Count > 0)
        {
            int shown = Math.Min(errors.Count, 10);
            string tail = errors.Count > 10 ? $"\n… and {errors.Count - 10} more." : string.Empty;
            summary += $"\n\nError(s):\n" + string.Join("\n", errors.Take(10)) + tail;
        }

        TaskDialog.Show("Amentum — Sync Complete", summary);
        return Result.Succeeded;
    }
}
