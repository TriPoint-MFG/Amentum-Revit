using System.Reflection;
using System.IO;
using System.Text.Json;
using AmentumRevit.Models;

namespace AmentumRevit.Services;

/// <summary>
/// Loads and caches shared_parameter_map.json from the add-in's DLL folder.
/// Falls back to a default name-based map if the file is missing or malformed.
/// </summary>
public static class ParameterMapService
{
    private static readonly object _lock = new();
    private static ParameterMap? _cached;

    public static ParameterMap LoadOrDefault()
    {
        lock (_lock)
        {
            if (_cached is not null)
                return _cached;

            string path = GetExpectedMapPath();

            if (File.Exists(path))
            {
                try
                {
                    var json = File.ReadAllText(path);
                    var map = JsonSerializer.Deserialize<ParameterMap>(json, JsonOpts);
                    if (map is not null)
                    {
                        _cached = Normalize(map);
                        return _cached;
                    }
                }
                catch { /* fall through to default */ }
            }

            _cached = GetDefaultMap();
            return _cached;
        }
    }

    public static string GetExpectedMapPath()
    {
        string dir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)
                     ?? Environment.CurrentDirectory;
        return Path.Combine(dir, "shared_parameter_map.json");
    }

    /// <summary>Clears the cache so the next LoadOrDefault() reloads from disk.</summary>
    public static void ResetCache()
    {
        lock (_lock) { _cached = null; }
    }

    /// <summary>
    /// Resolves a semantic key or raw name/GUID to a ParameterSpec.
    /// Resolution order:
    ///   1. keyOrRevitName is a GUID string → treat as direct GUID spec.
    ///   2. Matches a key in map.Parameters.
    ///   3. Fallback: treat as a raw Revit display name.
    /// </summary>
    public static ParameterSpec Resolve(ParameterMap map, string keyOrRevitName)
    {
        if (string.IsNullOrWhiteSpace(keyOrRevitName))
            return new ParameterSpec { Guid = null, RevitName = null };

        if (Guid.TryParse(keyOrRevitName.Trim(), out _))
            return new ParameterSpec { Guid = keyOrRevitName.Trim(), RevitName = null };

        if (map.Parameters.TryGetValue(keyOrRevitName.Trim(), out var spec))
            return spec;

        return new ParameterSpec { Guid = null, RevitName = keyOrRevitName.Trim() };
    }

    // ── Private ───────────────────────────────────────────────────────────────

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    private static ParameterMap Normalize(ParameterMap map)
    {
        map.ExcelExportKeys ??= new List<string>();
        map.WebBagKeys ??= new List<string>();

        if (map.ExcelExportKeys.Count == 0 && map.Parameters.Count > 0)
            map.ExcelExportKeys = map.Parameters.Keys.ToList();
        if (map.WebBagKeys.Count == 0 && map.Parameters.Count > 0)
            map.WebBagKeys = map.Parameters.Keys.ToList();

        return map;
    }

    private static ParameterMap GetDefaultMap()
    {
        // Conservative fallback — name-based only. Replace with real GUIDs via Build Map.
        return new ParameterMap
        {
            Version = 1,
            Parameters = new Dictionary<string, ParameterSpec>(StringComparer.OrdinalIgnoreCase)
            {
                ["Tag"]           = new() { RevitName = "Mark" },
                ["Comments"]      = new() { RevitName = "Comments" },
                ["VoltageV"]      = new() { RevitName = "Voltage (V)" },
                ["CurrentA"]      = new() { RevitName = "Current (A)" },
                ["Phase"]         = new() { RevitName = "Phase" },
                ["ConductorType"] = new() { RevitName = "Conductor Type" },
                ["ConduitSize"]   = new() { RevitName = "Conduit Size" },
                ["Panel"]         = new() { RevitName = "Panel" },
                ["CircuitNumber"] = new() { RevitName = "Circuit Number" },
                ["CalculatedVA"]  = new() { RevitName = "Calculated VA" },
                // DWG round-trip tracking fields (write the matched DWG handle/layer here):
                ["DwgHandle"]     = new() { RevitName = "Amentum DWG Handle", Notes = "Set by ImportFromDwgJsonCommand" },
                ["DwgLayer"]      = new() { RevitName = "Amentum DWG Layer",  Notes = "Set by ImportFromDwgJsonCommand" },
            },
            ExcelExportKeys = new List<string>
            {
                "Tag","Comments","VoltageV","CurrentA","Phase",
                "ConductorType","ConduitSize","Panel","CircuitNumber"
            },
            WebBagKeys = new List<string>
            {
                "Tag","Comments","VoltageV","CurrentA","Phase",
                "ConductorType","ConduitSize","Panel","CircuitNumber"
            }
        };
    }
}
