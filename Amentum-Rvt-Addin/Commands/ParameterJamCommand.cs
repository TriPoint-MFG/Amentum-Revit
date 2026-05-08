using System.Text.RegularExpressions;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace AmentumRevit.Commands;

/// <summary>
/// Finds duplicate project-parameter bindings and optionally removes duplicate bindings,
/// leaving one canonical definition per normalized parameter name.
/// </summary>
[Transaction(TransactionMode.Manual)]
public class ParameterJamCommand : IExternalCommand
{
    private sealed record BoundParameter(Definition Definition, ElementBinding? Binding, string Name, string NormalizedName);

    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        var doc = commandData.Application.ActiveUIDocument.Document;
        var bound = ReadProjectBindings(doc);
        var duplicateGroups = bound
            .GroupBy(p => p.NormalizedName, StringComparer.OrdinalIgnoreCase)
            .Where(g => !string.IsNullOrWhiteSpace(g.Key) && g.Count() > 1)
            .OrderBy(g => g.First().Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (duplicateGroups.Count == 0)
        {
            TaskDialog.Show("Amentum - Parameter Jam",
                $"No duplicate project parameter bindings found.\n\nProject parameter bindings scanned: {bound.Count}");
            return Result.Succeeded;
        }

        string preview = string.Join("\n", duplicateGroups.Take(8).Select(g =>
        {
            string names = string.Join(", ", g.Select(p => p.Name).Distinct(StringComparer.OrdinalIgnoreCase).Take(4));
            return $"- {names} ({g.Count()} bindings)";
        }));
        if (duplicateGroups.Count > 8)
            preview += $"\n...and {duplicateGroups.Count - 8} more duplicate name group(s).";

        var dialog = new TaskDialog("Amentum - Parameter Jam")
        {
            MainInstruction = "Duplicate project parameter bindings were found.",
            MainContent =
                preview +
                "\n\nChoose Yes to remove duplicate bindings and keep one canonical binding per normalized name. " +
                "Choose No to leave the project unchanged.",
            CommonButtons = TaskDialogCommonButtons.Yes | TaskDialogCommonButtons.No,
            DefaultButton = TaskDialogResult.No
        };

        if (dialog.Show() != TaskDialogResult.Yes)
            return Result.Cancelled;

        int removed = 0;
        var errors = new List<string>();

        using var tx = new Transaction(doc, "Amentum - Parameter Jam");
        tx.Start();

        foreach (var group in duplicateGroups)
        {
            var keep = ChooseKeeper(group);
            foreach (var duplicate in group.Where(p => !ReferenceEquals(p.Definition, keep.Definition)))
            {
                try
                {
                    if (doc.ParameterBindings.Remove(duplicate.Definition))
                        removed++;
                }
                catch (Exception ex)
                {
                    if (errors.Count < 8)
                        errors.Add($"{duplicate.Name}: {ex.Message}");
                }
            }
        }

        tx.Commit();

        string summary =
            $"Duplicate groups found: {duplicateGroups.Count}\n" +
            $"Duplicate bindings removed: {removed}\n" +
            $"Remaining canonical binding sets: {bound.Count - removed}";

        if (errors.Count > 0)
            summary += "\n\nCould not remove:\n- " + string.Join("\n- ", errors);

        TaskDialog.Show("Amentum - Parameter Jam", summary);
        return Result.Succeeded;
    }

    private static List<BoundParameter> ReadProjectBindings(Document doc)
    {
        var rows = new List<BoundParameter>();
        DefinitionBindingMapIterator iterator = doc.ParameterBindings.ForwardIterator();
        iterator.Reset();

        while (iterator.MoveNext())
        {
            if (iterator.Key is not Definition definition)
                continue;

            string name = definition.Name ?? string.Empty;
            rows.Add(new BoundParameter(
                definition,
                iterator.Current as ElementBinding,
                name,
                NormalizeName(name)));
        }

        return rows;
    }

    private static BoundParameter ChooseKeeper(IEnumerable<BoundParameter> group)
    {
        return group
            .OrderByDescending(p => p.Definition is ExternalDefinition)
            .ThenByDescending(p => p.Name.Contains("Amentum", StringComparison.OrdinalIgnoreCase))
            .ThenBy(p => p.Name.Length)
            .First();
    }

    private static string NormalizeName(string name)
    {
        string clean = Regex.Replace(name ?? string.Empty, @"\([^)]*\)", "");
        clean = Regex.Replace(clean, @"^(Amentum|AMTM|TRIP|TRIPOINT)[\s_\-]+", "", RegexOptions.IgnoreCase);
        clean = Regex.Replace(clean, @"[^A-Za-z0-9]+", "");
        return clean.ToLowerInvariant();
    }
}
