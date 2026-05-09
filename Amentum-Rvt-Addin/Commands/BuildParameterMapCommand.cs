using System.IO;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using AmentumRevit.Services;
using AmentumRevit.Views;
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

        var preview = new BuildParameterPreviewWindow(candidates);
        if (preview.ShowDialog() != true || preview.SelectedCandidates.Count == 0)
            return Result.Cancelled;

        var selectedCandidates = preview.SelectedCandidates;

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
                selectedCandidates);

            int mapped = CategorySharedParameterService.MergeCreatedDefinitionsIntoParameterMap(
                dialog.FileName,
                commandData.Application.Application);

            CategorySharedParameterBindResult? bindResult = null;
            if (preview.AddToProjectParameters)
            {
                using var tx = new Transaction(doc, "Amentum - Add Project Parameters");
                tx.Start();
                bindResult = CategorySharedParameterService.BindDefinitionsToProject(
                    doc,
                    commandData.Application.Application,
                    dialog.FileName,
                    selectedCandidates);
                tx.Commit();
            }

            string skippedPreview = string.Empty;
            if (result.SkippedMessages.Count > 0)
            {
                skippedPreview =
                    "\n\nSkipped examples:\n" +
                    string.Join("\n", result.SkippedMessages.Take(6));
                if (result.SkippedMessages.Count > 6)
                    skippedPreview += $"\n...and {result.SkippedMessages.Count - 6} more.";
            }

            string bindPreview = string.Empty;
            if (bindResult?.SkippedMessages.Count > 0)
            {
                bindPreview =
                    "\n\nProject parameter binding skipped examples:\n" +
                    string.Join("\n", bindResult.SkippedMessages.Take(6));
                if (bindResult.SkippedMessages.Count > 6)
                    bindPreview += $"\n...and {bindResult.SkippedMessages.Count - 6} more.";
            }

            string projectParameterSummary = preview.AddToProjectParameters
                ? $"Project parameters added: {bindResult?.Bound ?? 0}   Skipped: {bindResult?.Skipped ?? 0}\n"
                : "Project parameters added: 0 (file only)\n";

            TaskDialog.Show("Amentum - Build Parameters",
                "Shared parameter file generated.\n\n" +
                $"File: {dialog.FileName}\n" +
                $"Detected candidates: {candidates.Count}\n" +
                $"Selected candidates: {selectedCandidates.Count}\n" +
                $"Definitions created: {result.Created}\n" +
                $"Unsupported/skipped: {result.Skipped}\n" +
                $"Parameter-map rows merged: {mapped}\n" +
                projectParameterSummary +
                skippedPreview +
                bindPreview);

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
