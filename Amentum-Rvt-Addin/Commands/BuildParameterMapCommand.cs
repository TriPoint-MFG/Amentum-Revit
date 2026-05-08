using System.IO;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using AmentumRevit.Services;
using Microsoft.Win32;

namespace AmentumRevit.Commands;

/// <summary>
/// Builds a Revit shared parameter file from category/parameter candidates in the active model.
/// </summary>
[Transaction(TransactionMode.Manual)]
public class BuildParameterMapCommand : IExternalCommand
{
    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        var doc = commandData.Application.ActiveUIDocument.Document;
        var candidates = CategorySharedParameterService.Collect(doc);

        if (candidates.Count == 0)
        {
            TaskDialog.Show("Amentum - Build Parameters",
                "No category parameters were found in the active document.");
            return Result.Cancelled;
        }

        var dialog = new SaveFileDialog
        {
            Title = "Amentum - Create Shared Parameter File",
            Filter = "Revit Shared Parameter File (*.txt)|*.txt",
            FileName = $"{Sanitize(doc.Title)}_Amentum_SharedParameters.txt",
            AddExtension = true,
            DefaultExt = ".txt",
            OverwritePrompt = true
        };

        if (dialog.ShowDialog() != true)
            return Result.Cancelled;

        try
        {
            var result = CategorySharedParameterService.CreateSharedParameterFile(
                commandData.Application.Application,
                dialog.FileName,
                candidates);

            int mapped = CategorySharedParameterService.MergeCreatedDefinitionsIntoParameterMap(
                dialog.FileName,
                commandData.Application.Application);

            string skippedPreview = string.Empty;
            if (result.SkippedMessages.Count > 0)
            {
                skippedPreview =
                    "\n\nSkipped examples:\n" +
                    string.Join("\n", result.SkippedMessages.Take(6));
                if (result.SkippedMessages.Count > 6)
                    skippedPreview += $"\n...and {result.SkippedMessages.Count - 6} more.";
            }

            TaskDialog.Show("Amentum - Build Parameters",
                "Shared parameter file generated.\n\n" +
                $"File: {dialog.FileName}\n" +
                $"Detected candidates: {candidates.Count}\n" +
                $"Definitions created: {result.Created}\n" +
                $"Unsupported/skipped: {result.Skipped}\n" +
                $"Parameter-map rows merged: {mapped}\n\n" +
                "Use Revit's Project Parameters/Shared Parameters tools to bind the generated definitions to the categories you want." +
                skippedPreview);

            return Result.Succeeded;
        }
        catch (Exception ex)
        {
            message = ex.ToString();
            TaskDialog.Show("Amentum - Build Parameters", "Generation failed:\n" + ex.Message);
            return Result.Failed;
        }
    }

    private static string Sanitize(string value)
    {
        string output = string.IsNullOrWhiteSpace(value) ? "Project" : value.Trim();
        foreach (char ch in Path.GetInvalidFileNameChars())
            output = output.Replace(ch, '_');
        return output;
    }
}
