using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Autodesk.Revit.DB;
using AmentumRevit.Models;
using AmentumParameterMap = AmentumRevit.Models.ParameterMap;

namespace AmentumRevit.Services;

public static class DwgRoundTripService
{
    private static readonly string[] CadHandleParameterNames =
    {
        "Amentum DWG Handle",
        "DWG Handle",
        "DwgHandle",
        "BIMGUI_CAD_HANDLE",
        "CADHandle",
        "CAD Handle",
        "AutoCAD Handle"
    };

    public static DwgRoundTripResult Apply(Document doc, string importPath, AmentumParameterMap map)
    {
        var import = ReadImportFile(importPath);
        string? baselinePath = FindBaselinePath(importPath);
        var baseline = !string.IsNullOrWhiteSpace(baselinePath) && File.Exists(baselinePath)
            ? TryReadAmentumExchangeFile(baselinePath)
            : null;

        var baselineByUniqueId = (baseline?.Elements ?? new List<ExchangeElement>())
            .Where(e => !string.IsNullOrWhiteSpace(e.SourceId))
            .GroupBy(e => e.SourceId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var candidates = new FilteredElementCollector(doc)
            .WhereElementIsNotElementType()
            .Where(e => e?.Category is not null)
            .ToList();

        var handleSpec = ParameterMapService.Resolve(map, "DwgHandle");
        var layerSpec = ParameterMapService.Resolve(map, "DwgLayer");
        double scale = EnvironmentScanService.GetEnvDouble("AMENTUM_CAD_TO_REVIT_SCALE", 1.0);
        double minMove = EnvironmentScanService.GetEnvDouble("AMENTUM_DWG_MIN_MOVE_FT", 0.0001);

        int matched = 0;
        int moved = 0;
        int parameterWrites = 0;
        int unmatched = 0;
        var notes = new List<string>();

        using var tx = new Transaction(doc, "Amentum - Import DWG Sync");
        tx.Start();

        foreach (var cad in import.Objects)
        {
            XYZ? target = cad.Center();
            if (target is null)
            {
                unmatched++;
                continue;
            }

            Element? element = ResolveElement(doc, candidates, cad);
            if (element is null)
            {
                unmatched++;
                AddNote(notes, $"Unmatched CAD object {cad.SourceId} on layer {cad.Layer ?? "<none>"}.");
                continue;
            }

            matched++;

            string uniqueId = element.UniqueId;
            XYZ? baselinePoint = null;
            if (baselineByUniqueId.TryGetValue(uniqueId, out var baselineElement))
                baselinePoint = CenterOf(baselineElement);
            baselinePoint ??= CurrentCenter(element);

            if (baselinePoint is not null)
            {
                XYZ delta = new(
                    (target.X - baselinePoint.X) * scale,
                    (target.Y - baselinePoint.Y) * scale,
                    (target.Z - baselinePoint.Z) * scale);

                if (delta.GetLength() >= minMove)
                {
                    try
                    {
                        ElementTransformUtils.MoveElement(doc, element.Id, delta);
                        moved++;
                    }
                    catch (Exception ex)
                    {
                        unmatched++;
                        AddNote(notes, $"Could not move Revit element {element.Id.Value} for CAD object {cad.SourceId}: {ex.Message}");
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(cad.SourceId) &&
                RevitParameterUtil.TrySetParameterFromString(element, handleSpec, cad.SourceId, out _))
            {
                parameterWrites++;
            }

            if (!string.IsNullOrWhiteSpace(cad.Layer) &&
                RevitParameterUtil.TrySetParameterFromString(element, layerSpec, cad.Layer, out _))
            {
                parameterWrites++;
            }

            foreach (var kv in cad.Parameters)
            {
                if (!map.Parameters.ContainsKey(kv.Key))
                    continue;

                var spec = ParameterMapService.Resolve(map, kv.Key);
                if (RevitParameterUtil.TrySetParameterFromString(element, spec, kv.Value, out _))
                    parameterWrites++;
            }
        }

        tx.Commit();

        return new DwgRoundTripResult
        {
            ImportObjectCount = import.Objects.Count,
            BaselinePath = baselinePath,
            BaselineFound = baseline is not null,
            MatchedElementCount = matched,
            MovedElementCount = moved,
            ParameterWriteCount = parameterWrites,
            UnmatchedObjectCount = unmatched,
            Notes = notes
        };
    }

    private static DwgImportFile ReadImportFile(string path)
    {
        string raw = File.ReadAllText(path);
        using var json = JsonDocument.Parse(raw);

        if (json.RootElement.TryGetProperty("objects", out _))
        {
            var cadIndex = JsonSerializer.Deserialize<CadIndexFile>(raw, JsonOptions()) ?? new CadIndexFile();
            return new DwgImportFile(cadIndex.Objects.Select(o => DwgImportObject.FromCadIndex(o)).ToList());
        }

        var exchange = JsonSerializer.Deserialize<AmentumExchangeFile>(raw, JsonOptions())
            ?? throw new InvalidDataException("JSON deserialized to null.");

        return new DwgImportFile(exchange.Elements.Select(DwgImportObject.FromExchange).ToList());
    }

    private static AmentumExchangeFile? TryReadAmentumExchangeFile(string path)
    {
        try
        {
            string raw = File.ReadAllText(path);
            return JsonSerializer.Deserialize<AmentumExchangeFile>(raw, JsonOptions());
        }
        catch
        {
            return null;
        }
    }

    private static Element? ResolveElement(Document doc, IReadOnlyList<Element> candidates, DwgImportObject cad)
    {
        foreach (string? uniqueId in cad.PossibleRevitUniqueIds())
        {
            if (string.IsNullOrWhiteSpace(uniqueId))
                continue;

            Element? element = null;
            try { element = doc.GetElement(uniqueId); } catch { }
            if (element is not null)
                return element;
        }

        string handle = cad.SourceId.Trim();
        if (string.IsNullOrWhiteSpace(handle))
            return null;

        foreach (Element element in candidates)
        {
            foreach (string paramName in CadHandleParameterNames)
            {
                string? value = null;
                try { value = element.LookupParameter(paramName)?.AsString(); } catch { }
                if (!string.IsNullOrWhiteSpace(value) &&
                    string.Equals(value.Trim(), handle, StringComparison.OrdinalIgnoreCase))
                {
                    return element;
                }
            }
        }

        return null;
    }

    private static XYZ? CenterOf(ExchangeElement element)
    {
        if (element.Position is not null)
            return ToXyz(element.Position);
        if (element.BBox is not null)
            return new XYZ(
                (element.BBox.Min.X + element.BBox.Max.X) * 0.5,
                (element.BBox.Min.Y + element.BBox.Max.Y) * 0.5,
                (element.BBox.Min.Z + element.BBox.Max.Z) * 0.5);
        return null;
    }

    private static XYZ? CurrentCenter(Element element)
    {
        try
        {
            if (element.Location is LocationPoint lp)
                return lp.Point;
            if (element.Location is LocationCurve lc)
                return lc.Curve.Evaluate(0.5, true);
        }
        catch
        {
        }

        try
        {
            var box = element.get_BoundingBox(null);
            if (box is not null)
                return (box.Min + box.Max) * 0.5;
        }
        catch
        {
        }

        return null;
    }

    private static string? FindBaselinePath(string importPath)
    {
        string dir = Path.GetDirectoryName(importPath) ?? ".";
        string file = Path.GetFileName(importPath);
        string stem = Path.GetFileNameWithoutExtension(importPath);
        var candidates = new List<string>();

        if (stem.EndsWith("_cad_exchange", StringComparison.OrdinalIgnoreCase))
            candidates.Add(Path.Combine(dir, stem[..^"_cad_exchange".Length] + "_revit_exchange.json"));
        if (stem.EndsWith("_index", StringComparison.OrdinalIgnoreCase))
            candidates.Add(Path.Combine(dir, stem[..^"_index".Length] + "_revit_exchange.json"));
        if (stem.EndsWith("_revit_exchange", StringComparison.OrdinalIgnoreCase))
            candidates.Add(importPath);

        candidates.Add(importPath + ".baseline.json");

        foreach (string candidate in candidates.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (File.Exists(candidate))
                return candidate;
        }

        try
        {
            return Directory.GetFiles(dir, "*_revit_exchange.json", SearchOption.TopDirectoryOnly)
                .Select(p => new FileInfo(p))
                .OrderByDescending(f => f.LastWriteTimeUtc)
                .FirstOrDefault(f => !string.Equals(f.Name, file, StringComparison.OrdinalIgnoreCase))
                ?.FullName;
        }
        catch
        {
            return null;
        }
    }

    private static XYZ ToXyz(ExchangePoint3 point) => new(point.X, point.Y, point.Z);

    private static JsonSerializerOptions JsonOptions() => new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        WriteIndented = true
    };

    private static void AddNote(List<string> notes, string note)
    {
        if (notes.Count < 8)
            notes.Add(note);
    }

    private sealed record DwgImportFile(List<DwgImportObject> Objects);

    private sealed class DwgImportObject
    {
        public string SourceId { get; init; } = string.Empty;
        public string? RevitUniqueId { get; init; }
        public string? MatchedSourceId { get; init; }
        public string? Layer { get; init; }
        public string? EntityType { get; init; }
        public ExchangePoint3? Position { get; init; }
        public ExchangeBoundingBox? BBox { get; init; }
        public Dictionary<string, string?> Parameters { get; init; } = new();

        public static DwgImportObject FromExchange(ExchangeElement element)
        {
            return new DwgImportObject
            {
                SourceId = !string.IsNullOrWhiteSpace(element.Handle) ? element.Handle! : element.SourceId,
                RevitUniqueId = element.RevitUniqueId,
                MatchedSourceId = element.MatchedSourceId,
                Layer = element.Layer,
                EntityType = element.EntityType,
                Position = element.Position,
                BBox = element.BBox,
                Parameters = element.Parameters
            };
        }

        public static DwgImportObject FromCadIndex(CadObjectRecord record)
        {
            return new DwgImportObject
            {
                SourceId = record.Handle,
                RevitUniqueId = record.RevitUniqueId,
                Layer = record.Layer,
                EntityType = record.EntityType,
                Position = record.Center(),
                BBox = record.BBox is null
                    ? null
                    : new ExchangeBoundingBox
                    {
                        Min = new ExchangePoint3(record.BBox.Min.X, record.BBox.Min.Y, record.BBox.Min.Z),
                        Max = new ExchangePoint3(record.BBox.Max.X, record.BBox.Max.Y, record.BBox.Max.Z)
                    }
            };
        }

        public XYZ? Center()
        {
            if (Position is not null)
                return ToXyz(Position);
            if (BBox is not null)
                return new XYZ(
                    (BBox.Min.X + BBox.Max.X) * 0.5,
                    (BBox.Min.Y + BBox.Max.Y) * 0.5,
                    (BBox.Min.Z + BBox.Max.Z) * 0.5);
            return null;
        }

        public IEnumerable<string?> PossibleRevitUniqueIds()
        {
            yield return MatchedSourceId;
            yield return RevitUniqueId;

            if (Parameters.TryGetValue("revitUniqueId", out var p1))
                yield return p1;
            if (Parameters.TryGetValue("RevitUniqueId", out var p2))
                yield return p2;
            if (Parameters.TryGetValue("UniqueId", out var p3))
                yield return p3;

            string? fromLayer = ExtractRevitUniqueId(Layer);
            if (!string.IsNullOrWhiteSpace(fromLayer))
                yield return fromLayer;

            yield return SourceId;
        }

        private static string? ExtractRevitUniqueId(string? layer)
        {
            if (string.IsNullOrWhiteSpace(layer))
                return null;

            Match match = Regex.Match(layer, @"RVTUID[_\-](?<id>[A-Za-z0-9_.:\-]+)", RegexOptions.IgnoreCase);
            return match.Success ? match.Groups["id"].Value : null;
        }
    }

    private sealed class CadIndexFile
    {
        [JsonPropertyName("objects")]
        public List<CadObjectRecord> Objects { get; init; } = new();
    }

    private sealed class CadObjectRecord
    {
        [JsonPropertyName("handle")]
        public string Handle { get; init; } = string.Empty;

        [JsonPropertyName("layer")]
        public string? Layer { get; init; }

        [JsonPropertyName("entityType")]
        public string? EntityType { get; init; }

        [JsonPropertyName("bbox")]
        public ExchangeBoundingBox? BBox { get; init; }

        [JsonPropertyName("position")]
        public ExchangePoint3? Position { get; init; }

        [JsonPropertyName("revitUniqueId")]
        public string? RevitUniqueId { get; init; }

        [JsonPropertyName("geometry")]
        public CadGeometry Geometry { get; init; } = new();

        public ExchangePoint3? Center()
        {
            if (Position is not null)
                return Position;
            if (Geometry.Position is not null)
                return Geometry.Position;
            if (Geometry.Center is not null)
                return Geometry.Center;
            if (Geometry.Start is not null && Geometry.End is not null)
                return new ExchangePoint3(
                    (Geometry.Start.X + Geometry.End.X) * 0.5,
                    (Geometry.Start.Y + Geometry.End.Y) * 0.5,
                    (Geometry.Start.Z + Geometry.End.Z) * 0.5);
            if (BBox is not null)
                return new ExchangePoint3(
                    (BBox.Min.X + BBox.Max.X) * 0.5,
                    (BBox.Min.Y + BBox.Max.Y) * 0.5,
                    (BBox.Min.Z + BBox.Max.Z) * 0.5);
            return null;
        }
    }

    private sealed class CadGeometry
    {
        [JsonPropertyName("start")]
        public ExchangePoint3? Start { get; init; }

        [JsonPropertyName("end")]
        public ExchangePoint3? End { get; init; }

        [JsonPropertyName("center")]
        public ExchangePoint3? Center { get; init; }

        [JsonPropertyName("position")]
        public ExchangePoint3? Position { get; init; }
    }
}

public sealed class DwgRoundTripResult
{
    public int ImportObjectCount { get; init; }
    public string? BaselinePath { get; init; }
    public bool BaselineFound { get; init; }
    public int MatchedElementCount { get; init; }
    public int MovedElementCount { get; init; }
    public int ParameterWriteCount { get; init; }
    public int UnmatchedObjectCount { get; init; }
    public List<string> Notes { get; init; } = new();
}
