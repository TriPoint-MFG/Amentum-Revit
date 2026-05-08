using Autodesk.Revit.DB;
using ClosedXML.Excel;
using AmentumRevit.Models;

namespace AmentumRevit.Services;

public static class ExcelService
{
    /// <summary>
    /// Exports selected elements to a .xlsx file with three sheets:
    ///   Elements  — identity, geometry, electrical, mapped parameter columns
    ///   Updates   — empty template ready for round-trip edits
    ///   ParamMap  — the current shared_parameter_map.json as a reference sheet
    /// </summary>
    public static void ExportElements(string xlsxPath, Document doc, IList<Element> elements)
    {
        var map        = ParameterMapService.LoadOrDefault();
        var exportKeys = map.ExcelExportKeys ?? map.Parameters.Keys.ToList();

        using var wb = new XLWorkbook();

        // ── Elements sheet ────────────────────────────────────────────────────
        var ws = wb.AddWorksheet("Elements");
        int c  = 1;

        // Fixed identity columns
        ws.Cell(1, c++).Value = "UniqueId";
        ws.Cell(1, c++).Value = "ElementId";
        ws.Cell(1, c++).Value = "Category";
        ws.Cell(1, c++).Value = "Name";
        ws.Cell(1, c++).Value = "Family";
        ws.Cell(1, c++).Value = "Type";
        ws.Cell(1, c++).Value = "Tag";
        ws.Cell(1, c++).Value = "Comments";

        // Location
        ws.Cell(1, c++).Value = "LocType";
        ws.Cell(1, c++).Value = "X";
        ws.Cell(1, c++).Value = "Y";
        ws.Cell(1, c++).Value = "Z";
        ws.Cell(1, c++).Value = "StartX";
        ws.Cell(1, c++).Value = "StartY";
        ws.Cell(1, c++).Value = "StartZ";
        ws.Cell(1, c++).Value = "EndX";
        ws.Cell(1, c++).Value = "EndY";
        ws.Cell(1, c++).Value = "EndZ";

        // Bounding box
        ws.Cell(1, c++).Value = "BBMinX";
        ws.Cell(1, c++).Value = "BBMinY";
        ws.Cell(1, c++).Value = "BBMinZ";
        ws.Cell(1, c++).Value = "BBMaxX";
        ws.Cell(1, c++).Value = "BBMaxY";
        ws.Cell(1, c++).Value = "BBMaxZ";
        ws.Cell(1, c++).Value = "Dx";
        ws.Cell(1, c++).Value = "Dy";
        ws.Cell(1, c++).Value = "Dz";

        // Electrical
        ws.Cell(1, c++).Value = "VoltageV";
        ws.Cell(1, c++).Value = "CurrentA";
        ws.Cell(1, c++).Value = "Phase";
        ws.Cell(1, c++).Value = "ConductorType";
        ws.Cell(1, c++).Value = "ConduitSize";
        ws.Cell(1, c++).Value = "Panel";
        ws.Cell(1, c++).Value = "CircuitNumber";

        int firstParamCol = c;
        foreach (var key in exportKeys)
            ws.Cell(1, c++).Value = key;

        // Style the header row
        var headerRow = ws.Row(1);
        headerRow.Style.Font.Bold = true;
        headerRow.Style.Fill.BackgroundColor = XLColor.FromHtml("#003865"); // Amentum navy
        headerRow.Style.Font.FontColor = XLColor.White;

        int r = 2;
        foreach (var el in elements)
        {
            var p = RevitElementPayloadBuilder.Build(el, map,
                bagKeys: exportKeys, includeGeometry: true, includeElectrical: true);

            int col = 1;
            ws.Cell(r, col++).Value = p.UniqueId;
            ws.Cell(r, col++).Value = p.ElementId;
            ws.Cell(r, col++).Value = p.Category  ?? string.Empty;
            ws.Cell(r, col++).Value = p.Name      ?? string.Empty;
            ws.Cell(r, col++).Value = p.Family    ?? string.Empty;
            ws.Cell(r, col++).Value = p.Type      ?? string.Empty;
            ws.Cell(r, col++).Value = p.Electrical?.Tag ?? p.Parameters.GetValueOrDefault("Tag") ?? string.Empty;
            ws.Cell(r, col++).Value = p.Parameters.GetValueOrDefault("Comments") ?? string.Empty;

            string locType = p.Geometry?.Location?.Type ?? string.Empty;
            ws.Cell(r, col++).Value = locType;
            ws.Cell(r, col++).Value = p.Geometry?.Location?.Point?.X ?? 0;
            ws.Cell(r, col++).Value = p.Geometry?.Location?.Point?.Y ?? 0;
            ws.Cell(r, col++).Value = p.Geometry?.Location?.Point?.Z ?? 0;
            ws.Cell(r, col++).Value = p.Geometry?.Location?.Start?.X ?? 0;
            ws.Cell(r, col++).Value = p.Geometry?.Location?.Start?.Y ?? 0;
            ws.Cell(r, col++).Value = p.Geometry?.Location?.Start?.Z ?? 0;
            ws.Cell(r, col++).Value = p.Geometry?.Location?.End?.X ?? 0;
            ws.Cell(r, col++).Value = p.Geometry?.Location?.End?.Y ?? 0;
            ws.Cell(r, col++).Value = p.Geometry?.Location?.End?.Z ?? 0;

            ws.Cell(r, col++).Value = p.Geometry?.BoundingBox?.Min.X ?? 0;
            ws.Cell(r, col++).Value = p.Geometry?.BoundingBox?.Min.Y ?? 0;
            ws.Cell(r, col++).Value = p.Geometry?.BoundingBox?.Min.Z ?? 0;
            ws.Cell(r, col++).Value = p.Geometry?.BoundingBox?.Max.X ?? 0;
            ws.Cell(r, col++).Value = p.Geometry?.BoundingBox?.Max.Y ?? 0;
            ws.Cell(r, col++).Value = p.Geometry?.BoundingBox?.Max.Z ?? 0;
            ws.Cell(r, col++).Value = p.Geometry?.Dimensions?.Dx ?? 0;
            ws.Cell(r, col++).Value = p.Geometry?.Dimensions?.Dy ?? 0;
            ws.Cell(r, col++).Value = p.Geometry?.Dimensions?.Dz ?? 0;

            ws.Cell(r, col++).Value = p.Electrical?.VoltageV      ?? 0;
            ws.Cell(r, col++).Value = p.Electrical?.CurrentA      ?? 0;
            ws.Cell(r, col++).Value = p.Electrical?.Phase         ?? string.Empty;
            ws.Cell(r, col++).Value = p.Electrical?.ConductorType ?? string.Empty;
            ws.Cell(r, col++).Value = p.Electrical?.ConduitSize   ?? string.Empty;
            ws.Cell(r, col++).Value = p.Electrical?.Panel         ?? string.Empty;
            ws.Cell(r, col++).Value = p.Electrical?.CircuitNumber ?? string.Empty;

            // Dynamic param columns
            int pk = firstParamCol;
            foreach (var key in exportKeys)
                ws.Cell(r, pk++).Value = p.Parameters.GetValueOrDefault(key) ?? string.Empty;

            r++;
        }

        ws.Columns().AdjustToContents();
        ws.SheetView.FreezeRows(1);

        // ── Updates sheet (round-trip template) ───────────────────────────────
        var upd = wb.AddWorksheet("Updates");
        upd.Cell(1, 1).Value = "UniqueId";
        upd.Cell(1, 2).Value = "ParamKey";
        upd.Cell(1, 3).Value = "ParamGuid";
        upd.Cell(1, 4).Value = "Value";
        upd.Row(1).Style.Font.Bold = true;
        upd.Cell(2, 1).Value = "<paste element UniqueId>";
        upd.Cell(2, 2).Value = "<stable key e.g. Comments or VoltageV>";
        upd.Cell(2, 3).Value = "<optional GUID override — leave blank to use key>";
        upd.Cell(2, 4).Value = "<value>";
        upd.Columns().AdjustToContents();
        upd.SheetView.FreezeRows(1);

        // ── ParamMap reference sheet ──────────────────────────────────────────
        var pm = wb.AddWorksheet("ParamMap");
        pm.Cell(1, 1).Value = "Key";
        pm.Cell(1, 2).Value = "Guid";
        pm.Cell(1, 3).Value = "RevitName";
        pm.Cell(1, 4).Value = "Notes";
        pm.Row(1).Style.Font.Bold = true;

        int mr = 2;
        foreach (var kv in map.Parameters.OrderBy(k => k.Key))
        {
            pm.Cell(mr, 1).Value = kv.Key;
            pm.Cell(mr, 2).Value = kv.Value.Guid      ?? string.Empty;
            pm.Cell(mr, 3).Value = kv.Value.RevitName ?? string.Empty;
            pm.Cell(mr, 4).Value = kv.Value.Notes     ?? string.Empty;
            mr++;
        }
        pm.Columns().AdjustToContents();
        pm.SheetView.FreezeRows(1);

        wb.SaveAs(xlsxPath);
    }

    /// <summary>
    /// Reads the "Updates" sheet and returns a list of pending parameter changes.
    /// Columns resolved by header name so column order does not matter.
    /// </summary>
    public static List<ExcelParameterUpdate> ReadUpdates(string xlsxPath)
    {
        using var wb = new XLWorkbook(xlsxPath);

        var ws = wb.Worksheets.FirstOrDefault(s =>
            string.Equals(s.Name, "Updates", StringComparison.OrdinalIgnoreCase));
        if (ws is null)
            return new List<ExcelParameterUpdate>();

        // Build header → column index map
        var headerToCol = ws.Row(1).CellsUsed()
            .ToDictionary(
                cell => cell.GetString().Trim(),
                cell => cell.Address.ColumnNumber,
                StringComparer.OrdinalIgnoreCase);

        int colUnique = headerToCol.GetValueOrDefault("UniqueId", 1);
        int colKey    = headerToCol.GetValueOrDefault("ParamKey",
                            headerToCol.GetValueOrDefault("Key",
                                headerToCol.GetValueOrDefault("Parameter", 2)));
        int colGuid   = headerToCol.GetValueOrDefault("ParamGuid",
                            headerToCol.GetValueOrDefault("Guid", 3));
        int colVal    = headerToCol.GetValueOrDefault("Value",
                            headerToCol.GetValueOrDefault("Val", 4));

        var results = new List<ExcelParameterUpdate>();
        foreach (var row in ws.RowsUsed().Skip(1))
        {
            string uid  = row.Cell(colUnique).GetString().Trim();
            string key  = row.Cell(colKey).GetString().Trim();
            string guid = row.Cell(colGuid).GetString().Trim();
            string val  = row.Cell(colVal).GetString();

            if (string.IsNullOrWhiteSpace(uid)) continue;
            if (string.IsNullOrWhiteSpace(key) && string.IsNullOrWhiteSpace(guid)) continue;

            results.Add(new ExcelParameterUpdate(uid, key, guid, val));
        }

        return results;
    }
}
