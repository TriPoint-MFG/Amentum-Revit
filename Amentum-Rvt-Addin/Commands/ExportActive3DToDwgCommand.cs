using System.Globalization;
using System.Text.Json;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using AmentumRevit.Models;
using AmentumRevit.Services;
using Microsoft.Win32;

namespace AmentumRevit.Commands;

/// <summary>
/// Exports the active 3-D view as a DWG file and, optionally, also writes an
/// Amentum Exchange JSON (AmentumExchangeFile) alongside it so AutoCAD can
/// import the Revit element metadata without a live connection.
///
/// Environment variables (all optional):
///   AMENTUM_DWG_EXPORT_SETUP   — name of a saved DWG/DXF export setup (default: first available)
///   AMENTUM_DWG_WRITE_JSON     — "1" / "true" to also write the exchange JSON (default: true)
/// </summary>
[Transaction(TransactionMode.ReadOnly)]
public class ExportActive3DToDwgCommand : IExternalCommand
{
    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        var doc = commandData.Application.ActiveUIDocument.Document;

        var activeView = doc.ActiveView;
        if (activeView is null || activeView.ViewType != ViewType.ThreeD)
        {
            TaskDialog.Show("Amentum — Export DWG",
                "The active view must be a 3-D view. Activate a 3D view and try again.");
            return Result.Cancelled;
        }

        var dlg = new SaveFileDialog
        {
            Title      = "Amentum — Export 3D View as DWG",
            Filter     = "AutoCAD Drawing (*.dwg)|*.dwg",
            FileName   = $"Amentum_{EnvironmentScanService.SafeFilePart(doc.Title)}_{DateTime.Now:yyyyMMdd_HHmm}.dwg",
            DefaultExt = "dwg",
            OverwritePrompt = true
        };

        if (dlg.ShowDialog() != true)
            return Result.Cancelled;

        string outDir  = System.IO.Path.GetDirectoryName(dlg.FileName) ?? ".";
        string outStem = System.IO.Path.GetFileNameWithoutExtension(dlg.FileName);

        try
        {
            // ── DWG export ────────────────────────────────────────────────────
            var exportOptions = new DWGExportOptions
            {
                MergedViews = true
            };

            // Apply named setup if configured
            string? setupName = Environment.GetEnvironmentVariable("AMENTUM_DWG_EXPORT_SETUP");
            if (!string.IsNullOrWhiteSpace(setupName))
            {
                var setups = BaseExportOptions.GetPredefinedSetupNames(doc);
                if (setups is not null && setups.Contains(setupName, StringComparer.OrdinalIgnoreCase))
                {
                    exportOptions = DWGExportOptions.GetPredefinedOptions(doc, setupName);
                    exportOptions.MergedViews = true;
                }
            }

            var viewIds = new List<ElementId> { activeView.Id };
            doc.Export(outDir, outStem, viewIds, exportOptions);

            // ── Optional exchange JSON ────────────────────────────────────────
            bool writeJson = true;
            string? writeJsonEnv = Environment.GetEnvironmentVariable("AMENTUM_DWG_WRITE_JSON");
            if (!string.IsNullOrWhiteSpace(writeJsonEnv))
                writeJson = writeJsonEnv.Trim() is "1" or "true" or "True" or "TRUE";

            string? jsonPath = null;
            if (writeJson)
            {
                jsonPath = System.IO.Path.Combine(outDir, outStem + "_revit_exchange.json");
                WriteExchangeJson(doc, activeView, jsonPath);
            }

            string msg = $"DWG exported to:\n{dlg.FileName}";
            if (jsonPath is not null)
                msg += $"\n\nRevit exchange JSON:\n{jsonPath}";

            TaskDialog.Show("Amentum — Export DWG", msg);
            return Result.Succeeded;
        }
        catch (Exception ex)
        {
            message = ex.Message;
            return Result.Failed;
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static void WriteExchangeJson(Document doc, View view, string jsonPath)
    {
        var map = ParameterMapService.LoadOrDefault();

        var collector = new FilteredElementCollector(doc, view.Id)
            .WhereElementIsNotElementType()
            .ToElements();

        var exchangeElements = new List<ExchangeElement>();

        foreach (var el in collector)
        {
            if (el?.Category is null) continue;
            if (el is View or RevitLinkInstance) continue;

            var payload = RevitElementPayloadBuilder.Build(
                el, map, includeGeometry: true, includeElectrical: false);

            ExchangeBoundingBox? bbox = null;
            ExchangePoint3? position = null;

            if (payload.Geometry?.BoundingBox is not null)
            {
                var bb = payload.Geometry.BoundingBox;
                bbox = new ExchangeBoundingBox
                {
                    Min = new ExchangePoint3(bb.Min.X, bb.Min.Y, bb.Min.Z),
                    Max = new ExchangePoint3(bb.Max.X, bb.Max.Y, bb.Max.Z)
                };
            }

            if (payload.Geometry?.Location?.Point is not null)
            {
                var pt = payload.Geometry.Location.Point;
                position = new ExchangePoint3(pt.X, pt.Y, pt.Z);
            }
            else if (payload.Geometry?.Location?.Start is not null)
            {
                var s = payload.Geometry.Location.Start;
                var e2 = payload.Geometry.Location.End;
                position = new ExchangePoint3(
                    ((s?.X ?? 0) + (e2?.X ?? 0)) * 0.5,
                    ((s?.Y ?? 0) + (e2?.Y ?? 0)) * 0.5,
                    ((s?.Z ?? 0) + (e2?.Z ?? 0)) * 0.5);
            }

            var parameters = new Dictionary<string, string?>();
            foreach (var kv in payload.Parameters.Where(kv => kv.Value is not null))
                parameters[kv.Key] = kv.Value;
            parameters["RevitUniqueId"] = el.UniqueId;

            var xe = new ExchangeElement
            {
                SourceId   = el.UniqueId,
                SourceType = "revit",
                Category   = payload.Category ?? string.Empty,
                Name       = payload.Name      ?? string.Empty,
                Family     = payload.Family,
                Type       = payload.Type,
                Layer      = $"RVTUID_{el.UniqueId}",
                RevitUniqueId = el.UniqueId,
                MatchedSourceId = el.UniqueId,
                BBox       = bbox,
                Position   = position,
                Parameters = parameters
            };

            exchangeElements.Add(xe);
        }

        var file = new AmentumExchangeFile
        {
            Source        = "revit",
            GeometryUnits = "ft",
            Drawing       = doc.Title,
            Elements      = exchangeElements
        };

        var json = JsonSerializer.Serialize(file, new JsonSerializerOptions { WriteIndented = true });
        System.IO.File.WriteAllText(jsonPath, json);
    }
}
