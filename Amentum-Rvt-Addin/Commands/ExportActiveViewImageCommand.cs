using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Microsoft.Win32;

namespace AmentumRevit.Commands;

[Transaction(TransactionMode.ReadOnly)]
public class ExportActiveViewImageCommand : IExternalCommand
{
    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        var doc = commandData.Application.ActiveUIDocument.Document;

        var activeView = doc.ActiveView;
        if (activeView is null)
        {
            TaskDialog.Show("Amentum — Export View", "No active view.");
            return Result.Cancelled;
        }

        var dlg = new SaveFileDialog
        {
            Title      = "Amentum — Export Active View as PNG",
            Filter     = "PNG Image (*.png)|*.png",
            FileName   = $"Amentum_View_{AmentumRevit.Services.EnvironmentScanService.SafeFilePart(activeView.Name)}_{DateTime.Now:yyyyMMdd_HHmm}.png",
            DefaultExt = "png",
            OverwritePrompt = true
        };

        if (dlg.ShowDialog() != true)
            return Result.Cancelled;

        try
        {
            var options = new ImageExportOptions
            {
                ExportRange         = ExportRange.CurrentView,
                HLRandWFViewsFileType = ImageFileType.PNG,
                ImageResolution     = ImageResolution.DPI_150,
                ZoomType            = ZoomFitType.FitToPage,
                PixelSize           = 2048,
                FilePath            = System.IO.Path.Combine(
                                          System.IO.Path.GetDirectoryName(dlg.FileName) ?? string.Empty,
                                          System.IO.Path.GetFileNameWithoutExtension(dlg.FileName))
            };

            doc.ExportImage(options);

            // Revit may append a suffix — look for the actual output file
            string expectedFile = dlg.FileName;
            if (!System.IO.File.Exists(expectedFile))
            {
                // Revit sometimes appends view name; find the closest match
                var dir  = System.IO.Path.GetDirectoryName(dlg.FileName) ?? ".";
                var stem = System.IO.Path.GetFileNameWithoutExtension(dlg.FileName);
                var found = System.IO.Directory.GetFiles(dir, stem + "*.png").FirstOrDefault();
                if (found is not null)
                    expectedFile = found;
            }

            TaskDialog.Show("Amentum — Export View",
                $"View exported to:\n{expectedFile}");

            return Result.Succeeded;
        }
        catch (Exception ex)
        {
            message = ex.Message;
            return Result.Failed;
        }
    }
}
