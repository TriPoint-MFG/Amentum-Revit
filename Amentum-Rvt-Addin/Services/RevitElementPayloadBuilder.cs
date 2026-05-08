using System.Globalization;
using System.Text.RegularExpressions;
using Autodesk.Revit.DB;
using AmentumRevit.Models;
using AmentumParameterMap = AmentumRevit.Models.ParameterMap;

namespace AmentumRevit.Services;

/// <summary>
/// Converts a Revit Element into a strongly-typed ElementPayload used by
/// the Sync Panel, Excel export, and DWG exchange.
/// </summary>
public static class RevitElementPayloadBuilder
{
    private static readonly Regex NumberRegex =
        new(@"[-+]?\d*\.?\d+(?:[eE][-+]?\d+)?", RegexOptions.Compiled);

    public static ElementPayload Build(
        Element el,
        AmentumParameterMap map,
        IEnumerable<string>? bagKeys = null,
        bool includeGeometry = true,
        bool includeElectrical = true)
    {
        bagKeys ??= map.WebBagKeys ?? map.Parameters.Keys.ToList();

        var payload = new ElementPayload
        {
            UniqueId  = el.UniqueId,
            ElementId = el.Id.Value,
            Category  = el.Category?.Name,
            Name      = el.Name
        };

        if (el is FamilyInstance fi)
        {
            payload = payload with
            {
                Family = fi.Symbol?.FamilyName,
                Type   = fi.Symbol?.Name
            };
        }

        // Parameter bag
        var paramDict = new Dictionary<string, string?>();
        foreach (var key in bagKeys)
        {
            var spec = ParameterMapService.Resolve(map, key);
            paramDict[key] = RevitParameterUtil.GetParameterValueAsString(el, spec);
        }

        payload = payload with { Parameters = paramDict };

        if (includeGeometry)
            payload = payload with { Geometry = ExtractGeometry(el) };

        if (includeElectrical)
            payload = payload with { Electrical = ExtractElectrical(el, map) };

        return payload;
    }

    // ── Private ───────────────────────────────────────────────────────────────

    private static GeometryPayload? ExtractGeometry(Element el)
    {
        LocationPayload? loc = null;

        if (el.Location is LocationPoint lp)
            loc = new LocationPayload { Type = "point", Point = ToPoint(lp.Point) };
        else if (el.Location is LocationCurve lc)
            loc = new LocationPayload
            {
                Type  = "curve",
                Start = ToPoint(lc.Curve.GetEndPoint(0)),
                End   = ToPoint(lc.Curve.GetEndPoint(1))
            };

        BoundingBoxPayload?  bb  = null;
        DimensionsPayload?   dim = null;

        BoundingBoxXYZ? raw = null;
        try { raw = el.get_BoundingBox(null); } catch { /* ignore */ }

        if (raw is not null)
        {
            bb  = new BoundingBoxPayload { Min = ToPoint(raw.Min), Max = ToPoint(raw.Max) };
            dim = new DimensionsPayload
            {
                Dx = raw.Max.X - raw.Min.X,
                Dy = raw.Max.Y - raw.Min.Y,
                Dz = raw.Max.Z - raw.Min.Z
            };
        }

        if (loc is null && bb is null) return null;

        return new GeometryPayload { Location = loc, BoundingBox = bb, Dimensions = dim };
    }

    private static ElectricalPayload? ExtractElectrical(Element el, AmentumParameterMap map)
    {
        string? tag           = GetStr(el, map, "Tag");
        string? panel         = GetStr(el, map, "Panel");
        string? circuit       = GetStr(el, map, "CircuitNumber");
        string? phase         = GetStr(el, map, "Phase");
        string? conductorType = GetStr(el, map, "ConductorType");
        string? conduitSize   = GetStr(el, map, "ConduitSize");
        double? voltageV      = GetDbl(el, map, "VoltageV");
        double? currentA      = GetDbl(el, map, "CurrentA");

        if (tag is null && panel is null && circuit is null && phase is null &&
            conductorType is null && conduitSize is null && voltageV is null && currentA is null)
            return null;

        return new ElectricalPayload
        {
            Tag           = tag,
            Panel         = panel,
            CircuitNumber = circuit,
            Phase         = phase,
            ConductorType = conductorType,
            ConduitSize   = conduitSize,
            VoltageV      = voltageV,
            CurrentA      = currentA
        };
    }

    private static string? GetStr(Element el, AmentumParameterMap map, string key)
    {
        var spec = ParameterMapService.Resolve(map, key);
        var p    = RevitParameterUtil.GetParameter(el, spec);
        if (p is null) return null;

        string? s = null;
        try { s = p.AsString(); }       catch { /* ignore */ }
        if (string.IsNullOrWhiteSpace(s))
        {
            try { s = p.AsValueString(); } catch { /* ignore */ }
        }
        return string.IsNullOrWhiteSpace(s) ? null : s.Trim();
    }

    private static double? GetDbl(Element el, AmentumParameterMap map, string key)
    {
        var spec = ParameterMapService.Resolve(map, key);
        var p    = RevitParameterUtil.GetParameter(el, spec);
        if (p is null) return null;

        try
        {
            if (p.StorageType == StorageType.Double)
                return p.AsDouble();
        }
        catch { /* ignore */ }

        string? s = null;
        try { s = p.AsValueString(); } catch { /* ignore */ }
        if (string.IsNullOrWhiteSpace(s))
        {
            try { s = p.AsString(); } catch { /* ignore */ }
        }
        if (string.IsNullOrWhiteSpace(s)) return null;

        var m = NumberRegex.Match(s);
        if (!m.Success) return null;

        if (double.TryParse(m.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
            return v;
        if (double.TryParse(m.Value, NumberStyles.Float, CultureInfo.CurrentCulture, out v))
            return v;
        return null;
    }

    private static Point3 ToPoint(XYZ p) => new() { X = p.X, Y = p.Y, Z = p.Z };
}
