using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using AmentumRevit.Services;
using Microsoft.Win32;

namespace AmentumRevit.Commands;

/// <summary>
/// Imports CAD exchange/index JSON and applies CAD movement back to mapped Revit elements.
/// </summary>
[Transaction(TransactionMode.Manual)]
public class ImportFromDwgJsonCommand : IExternalCommand
{
    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        var doc = commandData.Application.ActiveUIDocument.Document;

        var dlg = new OpenFileDialog
        {
            Title = "Amentum - Import DWG Sync JSON",
            Filter = "JSON Exchange or CAD Index (*.json)|*.json|All Files (*.*)|*.*",
            CheckFileExists = true
        };

        if (dlg.ShowDialog() != true)
            return Result.Cancelled;

        try
        {
            var map = ParameterMapService.LoadOrDefault();
            DwgRoundTripResult result = DwgRoundTripService.Apply(doc, dlg.FileName, map);

            string summary =
                "DWG sync complete.\n\n" +
                $"Imported CAD objects: {result.ImportObjectCount}\n" +
                $"Matched Revit elements: {result.MatchedElementCount}\n" +
                $"Moved Revit elements: {result.MovedElementCount}\n" +
                $"Parameter writes: {result.ParameterWriteCount}\n" +
                $"Unmatched CAD objects: {result.UnmatchedObjectCount}\n" +
                $"Baseline: {(result.BaselineFound ? result.BaselinePath : "not found; current Revit positions used")}";

            if (result.Notes.Count > 0)
                summary += "\n\nNotes:\n- " + string.Join("\n- ", result.Notes);

            TaskDialog.Show("Amentum - Import DWG", summary);
            return Result.Succeeded;
        }
        catch (Exception ex)
        {
            message = ex.ToString();
            TaskDialog.Show("Amentum - Import DWG", "DWG sync failed:\n" + ex.Message);
            return Result.Failed;
        }
    }
}
