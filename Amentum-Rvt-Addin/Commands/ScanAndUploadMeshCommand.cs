using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using AmentumRevit.Services;

namespace AmentumRevit.Commands;

/// <summary>
/// Scans the local environment around the active view / selection centre and exports
/// an OBJ triangle mesh.  Optionally uploads to the enterprise endpoint.
///
/// Environment variables:
///   AMENTUM_SCAN_RADIUS_FT      — scan radius in feet          (default: 30)
///   AMENTUM_SCAN_MAX_ELEMENTS   — max elements to include       (default: 500)
///   AMENTUM_SCAN_MAX_TRIANGLES  — max triangles in OBJ output   (default: 200000)
///   AMENTUM_UPLOAD_URL          — enterprise endpoint (optional; skipped if not set)
///   AMENTUM_UPLOAD_BEARER_TOKEN — bearer token for the endpoint (optional)
/// </summary>
[Transaction(TransactionMode.ReadOnly)]
public class ScanAndUploadMeshCommand : IExternalCommand
{
    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        var uidoc = commandData.Application.ActiveUIDocument;
        var doc   = uidoc.Document;

        // ── Resolve scan centre ───────────────────────────────────────────────
        XYZ? centre =
            EnvironmentScanService.TryGetSelectionCenter(uidoc) ??
            EnvironmentScanService.TryGetActiveUIViewCenter(uidoc);

        if (centre is null)
        {
            TaskDialog.Show("Amentum — Scan Mesh",
                "Could not determine a scan centre.\n" +
                "Select elements or activate a view with a defined zoom region.");
            return Result.Cancelled;
        }

        // ── Read config ───────────────────────────────────────────────────────
        double radius      = EnvironmentScanService.GetEnvDouble("AMENTUM_SCAN_RADIUS_FT",     30.0);
        int    maxElements = EnvironmentScanService.GetEnvInt   ("AMENTUM_SCAN_MAX_ELEMENTS",   500);
        int    maxTri      = EnvironmentScanService.GetEnvInt   ("AMENTUM_SCAN_MAX_TRIANGLES",  200_000);

        // ── Collect elements ──────────────────────────────────────────────────
        var scannedElements = EnvironmentScanService.CollectElements(doc, centre, radius, maxElements);

        if (scannedElements.Count == 0)
        {
            TaskDialog.Show("Amentum — Scan Mesh",
                $"No elements found within {radius} ft of the scan centre.");
            return Result.Cancelled;
        }

        // ── Export OBJ ────────────────────────────────────────────────────────
        string tempDir  = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), EnvironmentScanService.TempFolderName);
        string stem     = EnvironmentScanService.SafeFilePart(doc.Title);
        string outPath  = System.IO.Path.Combine(tempDir,
            $"Amentum_{stem}_{DateTime.Now:yyyyMMdd_HHmmss}.obj");

        try
        {
            EnvironmentScanService.ExportMeshObj(
                doc, scannedElements, outPath, maxTri,
                viewForDetail: doc.ActiveView);
        }
        catch (Exception ex)
        {
            message = $"OBJ export failed: {ex.Message}";
            return Result.Failed;
        }

        long fileSizeKb = new System.IO.FileInfo(outPath).Length / 1024;

        // ── Optional upload ───────────────────────────────────────────────────
        string uploadStatus = "Upload skipped (AMENTUM_UPLOAD_URL not set).";
        try
        {
            using var cts = new System.Threading.CancellationTokenSource(
                TimeSpan.FromSeconds(120));

            var upload = ModelUploadService.UploadAsync(outPath, cts.Token)
                         .GetAwaiter().GetResult();

            uploadStatus = upload.Response is not null
                ? $"Uploaded successfully. Model ID: {upload.Response.ModelId ?? "(none)"}"
                : upload.SkipReason ?? "Upload skipped.";
        }
        catch (Exception ex)
        {
            uploadStatus = $"Upload error: {ex.Message}";
        }

        TaskDialog.Show("Amentum — Scan Mesh",
            $"Elements scanned: {scannedElements.Count}\n" +
            $"OBJ file: {fileSizeKb} KB\n" +
            $"Path: {outPath}\n\n" +
            uploadStatus);

        return Result.Succeeded;
    }
}
