using System.Text.Json.Serialization;

namespace AmentumRevit.Models;

/// <summary>
/// Shared exchange format for Revit ↔ AutoCAD round-trip data migration.
///
/// Both the AmentumRevit add-in and the AmentumCadReader plug-in read and write
/// this format, enabling stable cross-discipline data flow without a server.
///
/// File naming convention: {ProjectName}_amentum_exchange_{timestamp}.json
/// </summary>
public sealed class AmentumExchangeFile
{
    [JsonPropertyName("version")]
    public int Version { get; init; } = 1;

    [JsonPropertyName("exportedUtc")]
    public string ExportedUtc { get; init; } = DateTime.UtcNow.ToString("o");

    /// <summary>"revit" or "autocad"</summary>
    [JsonPropertyName("source")]
    public string Source { get; init; } = "revit";

    /// <summary>Document title (Revit) or drawing name (AutoCAD).</summary>
    [JsonPropertyName("drawing")]
    public string? Drawing { get; init; }

    [JsonPropertyName("geometryUnits")]
    public string GeometryUnits { get; init; } = "ft";

    [JsonPropertyName("elements")]
    public List<ExchangeElement> Elements { get; init; } = new();
}

public sealed class ExchangeElement
{
    /// <summary>
    /// Revit UniqueId or AutoCAD entity handle.
    /// Used for stable round-trip matching when the same element is exported more than once.
    /// </summary>
    [JsonPropertyName("sourceId")]
    public string SourceId { get; init; } = string.Empty;

    /// <summary>"revit" or "autocad"</summary>
    [JsonPropertyName("sourceType")]
    public string SourceType { get; init; } = "revit";

    [JsonPropertyName("category")]
    public string? Category { get; init; }

    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("family")]
    public string? Family { get; init; }

    [JsonPropertyName("type")]
    public string? Type { get; init; }

    /// <summary>AutoCAD layer (if source is autocad).</summary>
    [JsonPropertyName("layer")]
    public string? Layer { get; init; }

    /// <summary>AutoCAD entity type name (if source is autocad).</summary>
    [JsonPropertyName("entityType")]
    public string? EntityType { get; init; }

    [JsonPropertyName("handle")]
    public string? Handle { get; init; }

    [JsonPropertyName("revitUniqueId")]
    public string? RevitUniqueId { get; init; }

    [JsonPropertyName("bbox")]
    public ExchangeBoundingBox? BBox { get; init; }

    [JsonPropertyName("position")]
    public ExchangePoint3? Position { get; init; }

    /// <summary>
    /// Parameter values keyed by stable Amentum semantic keys.
    /// AutoCAD side maps block attributes to these same keys for consistent round-tripping.
    /// </summary>
    [JsonPropertyName("parameters")]
    public Dictionary<string, string?> Parameters { get; init; } = new();

    /// <summary>
    /// If this element has been matched to an entity in the other system,
    /// the matched entity's sourceId is stored here for future delta-only syncs.
    /// </summary>
    [JsonPropertyName("matchedSourceId")]
    public string? MatchedSourceId { get; init; }
}

public sealed class ExchangeBoundingBox
{
    [JsonPropertyName("min")]
    public ExchangePoint3 Min { get; init; } = new(0, 0, 0);

    [JsonPropertyName("max")]
    public ExchangePoint3 Max { get; init; } = new(0, 0, 0);
}

public sealed record ExchangePoint3(
    [property: JsonPropertyName("x")] double X,
    [property: JsonPropertyName("y")] double Y,
    [property: JsonPropertyName("z")] double Z
);
