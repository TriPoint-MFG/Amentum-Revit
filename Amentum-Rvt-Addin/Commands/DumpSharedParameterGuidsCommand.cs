using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using AmentumRevit.Services;
using Microsoft.Win32;

namespace AmentumRevit.Commands;

/// <summary>
/// Dumps all shared parameters visible on selected elements (or a filtered scan)
/// to a CSV file.  Useful for discovering GUIDs to populate shared_parameter_map.json.
/// </summary>
[Transaction(TransactionMode.ReadOnly)]
public class DumpSharedParameterGuidsCommand : IExternalCommand
{
    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        var uidoc = commandData.Application.ActiveUIDocument;
        var doc   = uidoc.Document;

        // Prefer selection; fall back to first 200 non-type elements
        var ids = uidoc.Selection.GetElementIds();
        IList<Element> targets;

        if (ids.Count > 0)
        {
            targets = ids.Select(id => doc.GetElement(id))
                         .Where(el => el is not null)
                         .ToList();
        }
        else
        {
            targets = new FilteredElementCollector(doc)
                .WhereElementIsNotElementType()
                .Take(200)
                .ToList();
        }

        if (targets.Count == 0)
        {
            TaskDialog.Show("Amentum — Dump GUIDs", "No elements found.");
            return Result.Cancelled;
        }

        var dlg = new SaveFileDialog
        {
            Title      = "Amentum — Save Shared Parameter GUID Dump",
            Filter     = "CSV File (*.csv)|*.csv",
            FileName   = $"Amentum_SharedParams_{DateTime.Now:yyyyMMdd_HHmm}.csv",
            DefaultExt = "csv",
            OverwritePrompt = true
        };

        if (dlg.ShowDialog() != true)
            return Result.Cancelled;

        try
        {
            // Collect unique params across all sampled elements
            var seen = new Dictionary<Guid, (string Name, string Group, int Count)>();

            foreach (var el in targets)
            {
                foreach (Parameter p in el.Parameters)
                {
                    if (p?.Definition is not InternalDefinition idef) continue;
                    // Only shared parameters have a non-zero GUID
                    // (We test via ExternalDefinition, but we can also check if it is shared)
                    if (el.get_Parameter(idef) is not { } param) continue;

                    // Try to get shared GUID
                    Guid guid = Guid.Empty;
                    try
                    {
                        if (p.IsShared)
                            guid = p.GUID;
                    }
                    catch { /* ignore */ }

                    if (guid == Guid.Empty) continue;

                    string name  = p.Definition.Name ?? string.Empty;
                    string group = p.Definition.GetGroupTypeId().TypeId;

                    if (seen.TryGetValue(guid, out var existing))
                        seen[guid] = (existing.Name, existing.Group, existing.Count + 1);
                    else
                        seen[guid] = (name, group, 1);
                }
            }

            using var w = new System.IO.StreamWriter(dlg.FileName);
            w.WriteLine("GUID,Name,ParameterGroup,SeenOnElements");
            foreach (var kv in seen.OrderBy(k => k.Value.Name))
                w.WriteLine($"{kv.Key},{Csv(kv.Value.Name)},{Csv(kv.Value.Group)},{kv.Value.Count}");

            TaskDialog.Show("Amentum — Dump GUIDs",
                $"Dumped {seen.Count} shared parameter(s) from {targets.Count} element(s).\n\n{dlg.FileName}");

            return Result.Succeeded;
        }
        catch (Exception ex)
        {
            message = ex.Message;
            return Result.Failed;
        }
    }

    private static string Csv(string s)
    {
        if (s.Contains(',') || s.Contains('"') || s.Contains('\n'))
            return $"\"{s.Replace("\"", "\"\"")}\"";
        return s;
    }
}
