using System.IO;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using AmentumRevit.Services;

namespace AmentumRevit.Commands;

/// <summary>
/// Scans once and exports both geometry formats: OBJ mesh and PLY point cloud.
/// Uploads both files when AMENTUM_UPLOAD_URL is configured.
/// </summary>
[Transaction(TransactionMode.ReadOnly)]
public class ScanAndUploadCommand : IExternalCommand
{
    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        var uidoc = commandData.Application.ActiveUIDocument;
        var doc = uidoc.Document;

        XYZ? centre =
            EnvironmentScanService.TryGetSelectionCenter(uidoc) ??
            EnvironmentScanService.TryGetActiveUIViewCenter(uidoc);

        if (centre is null)
        {
            TaskDialog.Show("Amentum - Upload Mesh + Pts",
                "Could not determine a scan centre.\n" +
                "Select elements or activate a view with a defined zoom region.");
            return Result.Cancelled;
        }

        double radius = EnvironmentScanService.GetEnvDouble("AMENTUM_SCAN_RADIUS_FT", 30.0);
        int maxElements = EnvironmentScanService.GetEnvInt("AMENTUM_SCAN_MAX_ELEMENTS", 500);
        int maxTriangles = EnvironmentScanService.GetEnvInt("AMENTUM_SCAN_MAX_TRIANGLES", 200_000);
        int maxPoints = EnvironmentScanService.GetEnvInt("AMENTUM_SCAN_MAX_POINTS", 500_000);

        var scannedElements = EnvironmentScanService.CollectElements(doc, centre, radius, maxElements);
        if (scannedElements.Count == 0)
        {
            TaskDialog.Show("Amentum - Upload Mesh + Pts",
                $"No elements found within {radius} ft of the scan centre.");
            return Result.Cancelled;
        }

        string tempDir = Path.Combine(Path.GetTempPath(), EnvironmentScanService.TempFolderName);
        string stem = EnvironmentScanService.SafeFilePart(doc.Title);
        string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string objPath = Path.Combine(tempDir, $"Amentum_{stem}_{stamp}.obj");
        string plyPath = Path.Combine(tempDir, $"Amentum_{stem}_{stamp}.ply");

        try
        {
            EnvironmentScanService.ExportMeshObj(
                doc, scannedElements, objPath, maxTriangles,
                viewForDetail: doc.ActiveView);

            EnvironmentScanService.ExportPointsPly(
                doc, scannedElements, plyPath, maxPoints,
                viewForDetail: doc.ActiveView);
        }
        catch (Exception ex)
        {
            message = $"Scan export failed: {ex.Message}";
            return Result.Failed;
        }

        string meshUpload = UploadFile(objPath);
        string pointUpload = UploadFile(plyPath);

        long objKb = new FileInfo(objPath).Length / 1024;
        long plyKb = new FileInfo(plyPath).Length / 1024;

        TaskDialog.Show("Amentum - Upload Mesh + Pts",
            $"Elements scanned: {scannedElements.Count}\n" +
            $"OBJ mesh: {objKb} KB\n" +
            $"PLY points: {plyKb} KB\n\n" +
            $"OBJ path:\n{objPath}\n\n" +
            $"PLY path:\n{plyPath}\n\n" +
            $"Mesh upload: {meshUpload}\n" +
            $"Points upload: {pointUpload}");

        return Result.Succeeded;
    }

    private static string UploadFile(string path)
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(120));
            var upload = ModelUploadService.UploadAsync(path, cts.Token)
                .GetAwaiter()
                .GetResult();

            return upload.Response is not null
                ? $"uploaded, model ID {upload.Response.ModelId ?? "(none)"}"
                : upload.SkipReason ?? "skipped";
        }
        catch (Exception ex)
        {
            return $"error: {ex.Message}";
        }
    }
}
