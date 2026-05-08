using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using AmentumRevit.Services;
using Microsoft.Win32;

namespace AmentumRevit.Commands;

[Transaction(TransactionMode.ReadOnly)]
public class ExportSelectedToExcelCommand : IExternalCommand
{
    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        var uidoc = commandData.Application.ActiveUIDocument;
        var doc   = uidoc.Document;

        var ids = uidoc.Selection.GetElementIds();
        if (ids.Count == 0)
        {
            TaskDialog.Show("Amentum — Export", "No elements selected. Select one or more elements and try again.");
            return Result.Cancelled;
        }

        var dlg = new SaveFileDialog
        {
            Title            = "Amentum — Export Selected Elements",
            Filter           = "Excel Workbook (*.xlsx)|*.xlsx",
            FileName         = $"Amentum_Export_{doc.Title}_{DateTime.Now:yyyyMMdd_HHmm}.xlsx",
            DefaultExt       = "xlsx",
            OverwritePrompt  = true
        };

        if (dlg.ShowDialog() != true)
            return Result.Cancelled;

        try
        {
            var selected = ids.Select(id => doc.GetElement(id))
                              .Where(el => el is not null)
                              .ToList();

            ExcelService.ExportElements(dlg.FileName, doc, selected);

            TaskDialog.Show("Amentum — Export",
                $"Exported {selected.Count} element(s) to:\n{dlg.FileName}");

            return Result.Succeeded;
        }
        catch (Exception ex)
        {
            message = ex.Message;
            return Result.Failed;
        }
    }
}
