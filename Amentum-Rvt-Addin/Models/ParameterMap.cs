using System.Text.Json.Serialization;

namespace AmentumRevit.Models;

/// <summary>
/// Stable mapping between Amentum semantic keys (e.g. "VoltageV") and Revit parameters.
///
/// Prefer GUID-based mapping for Shared Parameters.
/// Use revitName as a fallback for built-in or project parameters.
/// </summary>
public sealed class ParameterMap
{
    [JsonPropertyName("version")]
    public int Version { get; set; } = 1;

    /// <summary>
    /// Keyed by a stable semantic key, e.g. "VoltageV", "ConduitSize", "Comments".
    /// </summary>
    [JsonPropertyName("parameters")]
    public Dictionary<string, ParameterSpec> Parameters { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Keys exported to Excel (in addition to fixed columns such as UniqueId, geometry, etc.).
    /// </summary>
    [JsonPropertyName("excelExportKeys")]
    public List<string>? ExcelExportKeys { get; set; } = new();

    /// <summary>
    /// Keys included in exchange payloads (Sync Panel, DWG exchange, optional API calls).
    /// </summary>
    [JsonPropertyName("webBagKeys")]
    public List<string>? WebBagKeys { get; set; } = new();
}

public sealed class ParameterSpec
{
    /// <summary>
    /// Shared Parameter GUID (string form). Leave blank to fall back to revitName.
    /// </summary>
    [JsonPropertyName("guid")]
    public string? Guid { get; init; }

    /// <summary>
    /// Revit parameter display name. Used as fallback when guid is blank or lookup fails.
    /// </summary>
    [JsonPropertyName("revitName")]
    public string? RevitName { get; init; }

    [JsonPropertyName("notes")]
    public string? Notes { get; init; }
}
