using Autodesk.Revit.DB;
using ClosedXML.Excel;
using AmentumRevit.Models;
using AmentumParameterMap = AmentumRevit.Models.ParameterMap;

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
        var selectedElements = elements.Where(el => el is not null).ToList();
        var columnGuide = new List<ExcelColumnGuide>();

        using var wb = new XLWorkbook();

        // ── Elements sheet ────────────────────────────────────────────────────
        var ws = wb.AddWorksheet("Elements");
        int c  = 1;

        // Fixed identity columns
        AddReferenceColumn(ws, columnGuide, ref c, "UniqueId", null,
            "Required element identity. Copy this value into Updates.UniqueId.");
        AddReferenceColumn(ws, columnGuide, ref c, "ElementId", null,
            "Revit runtime id. Import Excel matches by UniqueId instead.");
        AddReferenceColumn(ws, columnGuide, ref c, "Category", null,
            "Reference category exported from Revit. Revit category cannot be changed by this importer.");
        AddReferenceColumn(ws, columnGuide, ref c, "Name", null,
            "Reference element name. Use a mapped parameter key in Updates when a name-like parameter is editable.");
        AddReferenceColumn(ws, columnGuide, ref c, "Family", null,
            "Reference family name. Family/type swaps are not handled by this Excel importer.");
        AddReferenceColumn(ws, columnGuide, ref c, "Type", null,
            "Reference type name. Type changes are not handled by this Excel importer.");
        AddReferenceColumn(ws, columnGuide, ref c, "Tag", "Tag",
            "Reference value. To round-trip, add a row on Updates with ParamKey Tag.");
        AddReferenceColumn(ws, columnGuide, ref c, "Comments", "Comments",
            "Reference value. To round-trip, add a row on Updates with ParamKey Comments.");

        // Location
        AddReferenceColumn(ws, columnGuide, ref c, "LocType", null, "Reference geometry. Location edits are not handled by this Excel importer.");
        AddReferenceColumn(ws, columnGuide, ref c, "X", null, "Reference geometry. Location edits are not handled by this Excel importer.");
        AddReferenceColumn(ws, columnGuide, ref c, "Y", null, "Reference geometry. Location edits are not handled by this Excel importer.");
        AddReferenceColumn(ws, columnGuide, ref c, "Z", null, "Reference geometry. Location edits are not handled by this Excel importer.");
        AddReferenceColumn(ws, columnGuide, ref c, "StartX", null, "Reference geometry. Location edits are not handled by this Excel importer.");
        AddReferenceColumn(ws, columnGuide, ref c, "StartY", null, "Reference geometry. Location edits are not handled by this Excel importer.");
        AddReferenceColumn(ws, columnGuide, ref c, "StartZ", null, "Reference geometry. Location edits are not handled by this Excel importer.");
        AddReferenceColumn(ws, columnGuide, ref c, "EndX", null, "Reference geometry. Location edits are not handled by this Excel importer.");
        AddReferenceColumn(ws, columnGuide, ref c, "EndY", null, "Reference geometry. Location edits are not handled by this Excel importer.");
        AddReferenceColumn(ws, columnGuide, ref c, "EndZ", null, "Reference geometry. Location edits are not handled by this Excel importer.");

        // Bounding box
        AddReferenceColumn(ws, columnGuide, ref c, "BBMinX", null, "Reference bounding box. This is calculated by Revit and ignored by Import Excel.");
        AddReferenceColumn(ws, columnGuide, ref c, "BBMinY", null, "Reference bounding box. This is calculated by Revit and ignored by Import Excel.");
        AddReferenceColumn(ws, columnGuide, ref c, "BBMinZ", null, "Reference bounding box. This is calculated by Revit and ignored by Import Excel.");
        AddReferenceColumn(ws, columnGuide, ref c, "BBMaxX", null, "Reference bounding box. This is calculated by Revit and ignored by Import Excel.");
        AddReferenceColumn(ws, columnGuide, ref c, "BBMaxY", null, "Reference bounding box. This is calculated by Revit and ignored by Import Excel.");
        AddReferenceColumn(ws, columnGuide, ref c, "BBMaxZ", null, "Reference bounding box. This is calculated by Revit and ignored by Import Excel.");
        AddReferenceColumn(ws, columnGuide, ref c, "Dx", null, "Reference bounding box dimension. This is calculated by Revit and ignored by Import Excel.");
        AddReferenceColumn(ws, columnGuide, ref c, "Dy", null, "Reference bounding box dimension. This is calculated by Revit and ignored by Import Excel.");
        AddReferenceColumn(ws, columnGuide, ref c, "Dz", null, "Reference bounding box dimension. This is calculated by Revit and ignored by Import Excel.");

        // Electrical
        AddReferenceColumn(ws, columnGuide, ref c, "VoltageV", "VoltageV",
            "Reference electrical value. To round-trip, add a row on Updates with ParamKey VoltageV.");
        AddReferenceColumn(ws, columnGuide, ref c, "CurrentA", "CurrentA",
            "Reference electrical value. To round-trip, add a row on Updates with ParamKey CurrentA.");
        AddReferenceColumn(ws, columnGuide, ref c, "Phase", "Phase",
            "Reference electrical value. To round-trip, add a row on Updates with ParamKey Phase.");
        AddReferenceColumn(ws, columnGuide, ref c, "ConductorType", "ConductorType",
            "Reference electrical value. To round-trip, add a row on Updates with ParamKey ConductorType.");
        AddReferenceColumn(ws, columnGuide, ref c, "ConduitSize", "ConduitSize",
            "Reference electrical value. To round-trip, add a row on Updates with ParamKey ConduitSize.");
        AddReferenceColumn(ws, columnGuide, ref c, "Panel", "Panel",
            "Reference electrical value. To round-trip, add a row on Updates with ParamKey Panel.");
        AddReferenceColumn(ws, columnGuide, ref c, "CircuitNumber", "CircuitNumber",
            "Reference electrical value. To round-trip, add a row on Updates with ParamKey CircuitNumber.");

        int firstParamCol = c;
        foreach (var key in exportKeys)
            AddMappedColumn(ws, columnGuide, ref c, selectedElements, map, key);

        int r = 2;
        foreach (var el in selectedElements)
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
        upd.Cell(1, 5).Value = "Note";
        StyleHeaderRow(upd.Row(1), XLColor.FromHtml("#003865"));
        upd.Cell(2, 1).Value = "<paste element UniqueId>";
        upd.Cell(2, 2).Value = "<stable key e.g. Comments or VoltageV>";
        upd.Cell(2, 3).Value = "<optional GUID override - leave blank to use key>";
        upd.Cell(2, 4).Value = "<value>";
        upd.Cell(2, 5).Value = "Template row is ignored on import. Add real update rows below or replace this row.";
        upd.Columns().AdjustToContents();
        upd.SheetView.FreezeRows(1);

        AddEditGuideSheet(wb, columnGuide);
        AddUpdateStatusSheet(wb, selectedElements, map, exportKeys);

        // ── ParamMap reference sheet ──────────────────────────────────────────
        var pm = wb.AddWorksheet("ParamMap");
        pm.Cell(1, 1).Value = "Key";
        pm.Cell(1, 2).Value = "Guid";
        pm.Cell(1, 3).Value = "RevitName";
        pm.Cell(1, 4).Value = "Notes";
        StyleHeaderRow(pm.Row(1), XLColor.FromHtml("#003865"));

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
            if (IsTemplateValue(uid) || IsTemplateValue(key) || IsTemplateValue(guid)) continue;
            if (string.IsNullOrWhiteSpace(key) && string.IsNullOrWhiteSpace(guid)) continue;

            results.Add(new ExcelParameterUpdate(uid, key, guid, val));
        }

        return results;
    }

    private static void AddReferenceColumn(
        IXLWorksheet ws,
        List<ExcelColumnGuide> columnGuide,
        ref int column,
        string header,
        string? paramKey,
        string reason)
    {
        int currentColumn = column++;
        ws.Cell(1, currentColumn).Value = header;
        StyleElementHeader(ws.Cell(1, currentColumn), XLColor.FromHtml("#6B7280"));

        string howToEdit = paramKey is null
            ? "This column is reference-only for Excel import."
            : $"Add a row on Updates with the same UniqueId, ParamKey {paramKey}, and the new Value.";

        columnGuide.Add(new ExcelColumnGuide(
            currentColumn,
            header,
            paramKey,
            "No - Elements sheet edits are ignored by Import Excel",
            paramKey is null ? "Not imported" : "Use Updates sheet",
            reason,
            howToEdit));
    }

    private static void AddMappedColumn(
        IXLWorksheet ws,
        List<ExcelColumnGuide> columnGuide,
        ref int column,
        IList<Element> selectedElements,
        AmentumParameterMap map,
        string key)
    {
        int currentColumn = column++;
        var summary = AnalyzeParamKey(selectedElements, map, key);

        ws.Cell(1, currentColumn).Value = key;
        StyleElementHeader(ws.Cell(1, currentColumn), GetStatusColor(summary));

        columnGuide.Add(new ExcelColumnGuide(
            currentColumn,
            key,
            key,
            "No - Elements sheet edits are ignored by Import Excel",
            summary.Status,
            summary.Reason,
            $"Add a row on Updates with the same UniqueId, ParamKey {key}, and the new Value."));
    }

    private static void AddEditGuideSheet(XLWorkbook wb, IList<ExcelColumnGuide> columnGuide)
    {
        var guide = wb.AddWorksheet("Edit Guide");
        guide.Cell(1, 1).Value = "Column";
        guide.Cell(1, 2).Value = "Elements Header";
        guide.Cell(1, 3).Value = "ParamKey";
        guide.Cell(1, 4).Value = "Can I edit this cell directly?";
        guide.Cell(1, 5).Value = "Import Excel Status";
        guide.Cell(1, 6).Value = "Why";
        guide.Cell(1, 7).Value = "How to Round Trip";
        StyleHeaderRow(guide.Row(1), XLColor.FromHtml("#003865"));

        int r = 2;
        foreach (var item in columnGuide)
        {
            guide.Cell(r, 1).Value = $"{GetColumnLetter(item.ColumnNumber)} ({item.ColumnNumber})";
            guide.Cell(r, 2).Value = item.Header;
            guide.Cell(r, 3).Value = item.ParamKey ?? string.Empty;
            guide.Cell(r, 4).Value = item.DirectEditStatus;
            guide.Cell(r, 5).Value = item.ImportStatus;
            guide.Cell(r, 6).Value = item.Reason;
            guide.Cell(r, 7).Value = item.HowToRoundTrip;
            r++;
        }

        guide.Columns().AdjustToContents();
        guide.SheetView.FreezeRows(1);
    }

    private static void AddUpdateStatusSheet(
        XLWorkbook wb,
        IList<Element> selectedElements,
        AmentumParameterMap map,
        IList<string> exportKeys)
    {
        var status = wb.AddWorksheet("Update Status");
        status.Cell(1, 1).Value = "UniqueId";
        status.Cell(1, 2).Value = "ElementId";
        status.Cell(1, 3).Value = "Category";
        status.Cell(1, 4).Value = "Name";
        status.Cell(1, 5).Value = "ParamKey";
        status.Cell(1, 6).Value = "RevitName";
        status.Cell(1, 7).Value = "ParamGuid";
        status.Cell(1, 8).Value = "CurrentValue";
        status.Cell(1, 9).Value = "Can Import Change?";
        status.Cell(1, 10).Value = "Reason";
        StyleHeaderRow(status.Row(1), XLColor.FromHtml("#003865"));

        int r = 2;
        foreach (var el in selectedElements)
        {
            foreach (string key in exportKeys)
            {
                var item = EvaluateElementParameter(el, map, key);
                status.Cell(r, 1).Value = el.UniqueId;
                status.Cell(r, 2).Value = el.Id.Value;
                status.Cell(r, 3).Value = el.Category?.Name ?? string.Empty;
                status.Cell(r, 4).Value = el.Name ?? string.Empty;
                status.Cell(r, 5).Value = key;
                status.Cell(r, 6).Value = item.RevitName ?? string.Empty;
                status.Cell(r, 7).Value = item.Guid ?? string.Empty;
                status.Cell(r, 8).Value = item.CurrentValue ?? string.Empty;
                status.Cell(r, 9).Value = item.CanImportChange ? "Yes" : "No";
                status.Cell(r, 10).Value = item.Reason;
                status.Cell(r, 9).Style.Fill.BackgroundColor = item.CanImportChange
                    ? XLColor.FromHtml("#D9EAD3")
                    : XLColor.FromHtml("#F4CCCC");
                r++;
            }
        }

        status.Columns().AdjustToContents();
        status.SheetView.FreezeRows(1);
    }

    private static ParameterKeySummary AnalyzeParamKey(
        IList<Element> selectedElements,
        AmentumParameterMap map,
        string key)
    {
        if (selectedElements.Count == 0)
            return new ParameterKeySummary("No selected elements", "No elements were exported.", 0, 0, 0, 0, 0);

        var statuses = selectedElements
            .Select(el => EvaluateElementParameter(el, map, key))
            .ToList();

        int editable = statuses.Count(s => s.CanImportChange);
        int missing = statuses.Count(s => s.Reason.StartsWith("Parameter not found", StringComparison.OrdinalIgnoreCase));
        int readOnly = statuses.Count(s => s.Reason.StartsWith("Parameter is read-only", StringComparison.OrdinalIgnoreCase));
        int unsupported = statuses.Count(s => s.Reason.StartsWith("Unsupported", StringComparison.OrdinalIgnoreCase));
        int total = statuses.Count;

        string status = editable switch
        {
            0 => "Not editable for selected elements",
            _ when editable == total => "Editable for selected elements",
            _ => "Partially editable for selected elements"
        };

        string reason = editable == total
            ? "All selected elements have a writable parameter with a supported storage type."
            : $"Editable: {editable}; missing: {missing}; read-only: {readOnly}; unsupported: {unsupported}; selected elements: {total}. See Update Status for element-by-element detail.";

        return new ParameterKeySummary(status, reason, editable, missing, readOnly, unsupported, total);
    }

    private static ElementParameterStatus EvaluateElementParameter(Element element, AmentumParameterMap map, string key)
    {
        var spec = ParameterMapService.Resolve(map, key);
        var p = RevitParameterUtil.GetParameter(element, spec);
        string? currentValue = RevitParameterUtil.GetParameterValueAsString(element, spec);

        if (p is null)
        {
            return new ElementParameterStatus(
                key,
                spec.RevitName,
                spec.Guid,
                currentValue,
                false,
                "Parameter not found on this element");
        }

        if (p.IsReadOnly)
        {
            return new ElementParameterStatus(
                key,
                spec.RevitName ?? p.Definition?.Name,
                spec.Guid,
                currentValue,
                false,
                "Parameter is read-only in Revit");
        }

        if (p.StorageType == StorageType.None)
        {
            return new ElementParameterStatus(
                key,
                spec.RevitName ?? p.Definition?.Name,
                spec.Guid,
                currentValue,
                false,
                "Unsupported parameter storage type");
        }

        return new ElementParameterStatus(
            key,
            spec.RevitName ?? p.Definition?.Name,
            spec.Guid,
            currentValue,
            true,
            $"Editable through Updates sheet ({p.StorageType})");
    }

    private static void StyleElementHeader(IXLCell cell, XLColor color)
    {
        cell.Style.Font.Bold = true;
        cell.Style.Font.FontColor = XLColor.White;
        cell.Style.Fill.BackgroundColor = color;
        cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        cell.Style.Border.BottomBorder = XLBorderStyleValues.Thin;
    }

    private static void StyleHeaderRow(IXLRow row, XLColor color)
    {
        row.Style.Font.Bold = true;
        row.Style.Font.FontColor = XLColor.White;
        row.Style.Fill.BackgroundColor = color;
    }

    private static XLColor GetStatusColor(ParameterKeySummary summary)
    {
        if (summary.TotalCount == 0 || summary.EditableCount == 0)
            return XLColor.FromHtml("#B91C1C");
        if (summary.EditableCount == summary.TotalCount)
            return XLColor.FromHtml("#0F766E");
        return XLColor.FromHtml("#B45309");
    }

    private static bool IsTemplateValue(string value)
    {
        string trimmed = value.Trim();
        return trimmed.Length >= 2 && trimmed.StartsWith("<", StringComparison.Ordinal) && trimmed.EndsWith(">", StringComparison.Ordinal);
    }

    private static string GetColumnLetter(int columnNumber)
    {
        string letter = string.Empty;
        while (columnNumber > 0)
        {
            int modulo = (columnNumber - 1) % 26;
            letter = Convert.ToChar('A' + modulo) + letter;
            columnNumber = (columnNumber - modulo) / 26;
        }
        return letter;
    }

    private sealed record ExcelColumnGuide(
        int ColumnNumber,
        string Header,
        string? ParamKey,
        string DirectEditStatus,
        string ImportStatus,
        string Reason,
        string HowToRoundTrip);

    private sealed record ParameterKeySummary(
        string Status,
        string Reason,
        int EditableCount,
        int MissingCount,
        int ReadOnlyCount,
        int UnsupportedCount,
        int TotalCount);

    private sealed record ElementParameterStatus(
        string Key,
        string? RevitName,
        string? Guid,
        string? CurrentValue,
        bool CanImportChange,
        string Reason);
}
