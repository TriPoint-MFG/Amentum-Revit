using System.Text.Json.Serialization;

namespace AmentumRevit.Models;

// ─────────────────────────────────────────────────────────────────────────────
// Exchange contracts shared between the WPF Sync Panel, the Excel service,
// and any optional enterprise API integration.
// ─────────────────────────────────────────────────────────────────────────────

public sealed class SyncRequest
{
    [JsonPropertyName("elements")]
    public List<ElementPayload> Elements { get; init; } = new();

    [JsonPropertyName("geometryUnits")]
    public string GeometryUnits { get; init; } = "ft";

    [JsonPropertyName("document")]
    public DocumentMetadata? Document { get; init; }
}

public sealed class DocumentMetadata
{
    [JsonPropertyName("title")]
    public string? Title { get; init; }

    [JsonPropertyName("pathName")]
    public string? PathName { get; init; }

    [JsonPropertyName("projectNumber")]
    public string? ProjectNumber { get; init; }

    [JsonPropertyName("timestampUtc")]
    public string? TimestampUtc { get; init; }
}

public sealed record ElementPayload
{
    [JsonPropertyName("uniqueId")]
    public string UniqueId { get; init; } = string.Empty;

    [JsonPropertyName("elementId")]
    public long ElementId { get; init; }

    [JsonPropertyName("category")]
    public string? Category { get; init; }

    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("family")]
    public string? Family { get; init; }

    [JsonPropertyName("type")]
    public string? Type { get; init; }

    [JsonPropertyName("geometry")]
    public GeometryPayload? Geometry { get; init; }

    [JsonPropertyName("electrical")]
    public ElectricalPayload? Electrical { get; init; }

    /// <summary>
    /// Keyed by stable semantic keys (e.g. "VoltageV", "Panel").
    /// Values are always strings to avoid unit/type ambiguity across systems.
    /// </summary>
    [JsonPropertyName("parameters")]
    public Dictionary<string, string?> Parameters { get; init; } = new();
}

public sealed class GeometryPayload
{
    [JsonPropertyName("location")]
    public LocationPayload? Location { get; init; }

    [JsonPropertyName("boundingBox")]
    public BoundingBoxPayload? BoundingBox { get; init; }

    [JsonPropertyName("dimensions")]
    public DimensionsPayload? Dimensions { get; init; }
}

public sealed class LocationPayload
{
    /// <summary>"point" or "curve"</summary>
    [JsonPropertyName("type")]
    public string Type { get; init; } = "point";

    [JsonPropertyName("point")]
    public Point3? Point { get; init; }

    [JsonPropertyName("start")]
    public Point3? Start { get; init; }

    [JsonPropertyName("end")]
    public Point3? End { get; init; }
}

public sealed class BoundingBoxPayload
{
    [JsonPropertyName("min")]
    public Point3 Min { get; init; } = new();

    [JsonPropertyName("max")]
    public Point3 Max { get; init; } = new();
}

public sealed class DimensionsPayload
{
    [JsonPropertyName("dx")]
    public double Dx { get; init; }

    [JsonPropertyName("dy")]
    public double Dy { get; init; }

    [JsonPropertyName("dz")]
    public double Dz { get; init; }
}

public sealed class Point3
{
    [JsonPropertyName("x")]
    public double X { get; init; }

    [JsonPropertyName("y")]
    public double Y { get; init; }

    [JsonPropertyName("z")]
    public double Z { get; init; }
}

public sealed class ElectricalPayload
{
    [JsonPropertyName("voltageV")]
    public double? VoltageV { get; init; }

    [JsonPropertyName("currentA")]
    public double? CurrentA { get; init; }

    /// <summary>"1P" or "3P"</summary>
    [JsonPropertyName("phase")]
    public string? Phase { get; init; }

    [JsonPropertyName("conductorType")]
    public string? ConductorType { get; init; }

    [JsonPropertyName("conduitSize")]
    public string? ConduitSize { get; init; }

    [JsonPropertyName("panel")]
    public string? Panel { get; init; }

    [JsonPropertyName("circuitNumber")]
    public string? CircuitNumber { get; init; }

    [JsonPropertyName("tag")]
    public string? Tag { get; init; }
}

public sealed class SyncResponse
{
    [JsonPropertyName("updates")]
    public List<ParameterUpdate> Updates { get; init; } = new();

    [JsonPropertyName("warnings")]
    public List<SyncWarning> Warnings { get; init; } = new();
}

public sealed class SyncWarning
{
    [JsonPropertyName("uniqueId")]
    public string? UniqueId { get; init; }

    [JsonPropertyName("message")]
    public string Message { get; init; } = string.Empty;
}

public sealed class ParameterUpdate
{
    [JsonPropertyName("uniqueId")]
    public string UniqueId { get; init; } = string.Empty;

    /// <summary>Preferred: stable semantic key resolved via shared_parameter_map.json.</summary>
    [JsonPropertyName("parameterKey")]
    public string? ParameterKey { get; init; }

    /// <summary>Direct Shared Parameter GUID. Overrides parameterKey when present.</summary>
    [JsonPropertyName("parameterGuid")]
    public string? ParameterGuid { get; init; }

    /// <summary>Fallback Revit display name if GUID lookup fails.</summary>
    [JsonPropertyName("parameterName")]
    public string? ParameterName { get; init; }

    [JsonPropertyName("value")]
    public string? Value { get; init; }
}

// ─────────────────────────────────────────────────────────────────────────────
// Model upload response (for optional enterprise endpoint integration).
// ─────────────────────────────────────────────────────────────────────────────

public sealed class UploadModelResponse
{
    [JsonPropertyName("modelId")]
    public string? ModelId { get; init; }

    [JsonPropertyName("filename")]
    public string? Filename { get; init; }

    [JsonPropertyName("storedPath")]
    public string? StoredPath { get; init; }
}
