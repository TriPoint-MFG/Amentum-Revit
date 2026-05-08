using System.IO;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Autodesk.Revit.DB;
using AmentumRevit.Models;

namespace AmentumRevit.Services;

public static class CategorySharedParameterService
{
    public static List<CategorySharedParameterCandidate> Collect(Document doc)
    {
        var result = new Dictionary<string, CategorySharedParameterCandidate>(StringComparer.OrdinalIgnoreCase);
        int maxElements = EnvironmentScanService.GetEnvInt("AMENTUM_BUILD_PARAMETERS_MAX_ELEMENTS", 5000);

        var elements = new FilteredElementCollector(doc)
            .WhereElementIsNotElementType()
            .Where(e => e?.Category is not null)
            .Take(maxElements)
            .ToList();

        foreach (Element element in elements)
        {
            string category = CleanGroupName(element.Category?.Name);
            if (string.IsNullOrWhiteSpace(category))
                continue;

            CollectFromElement(result, category, element, "instance");

            Element? typeElement = null;
            try
            {
                var typeId = element.GetTypeId();
                if (typeId != ElementId.InvalidElementId)
                    typeElement = doc.GetElement(typeId);
            }
            catch
            {
            }

            if (typeElement is not null)
                CollectFromElement(result, category, typeElement, "type");
        }

        return result.Values
            .OrderBy(r => r.GroupName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(r => r.ParameterName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static CategorySharedParameterCreateResult CreateSharedParameterFile(
        Autodesk.Revit.ApplicationServices.Application app,
        string path,
        IReadOnlyList<CategorySharedParameterCandidate> candidates)
    {
        string oldSharedParameterPath = app.SharedParametersFilename;
        var skipped = new List<string>();

        try
        {
            DefinitionFile file = OpenOrRecreateSharedParameterFile(app, path, skipped);

            int created = 0;
            foreach (CategorySharedParameterCandidate row in candidates)
            {
                if (!IsCreatableSharedParameterDataType(row.DataType, out string reason))
                {
                    skipped.Add($"{row.GroupName} / {row.ParameterName}: unsupported data type {DataTypeLabel(row.DataType)} ({reason}).");
                    continue;
                }

                try
                {
                    DefinitionGroup group = GetOrCreateGroup(file, row.GroupName);
                    if (HasDefinition(group, row.ParameterName))
                        continue;

                    var options = new ExternalDefinitionCreationOptions(row.ParameterName, row.DataType)
                    {
                        Description = row.Description,
                        UserModifiable = true,
                        Visible = true
                    };
                    group.Definitions.Create(options);
                    created++;
                }
                catch (Exception ex)
                {
                    skipped.Add($"{row.GroupName} / {row.ParameterName}: Revit could not create it ({ex.Message}).");
                }
            }

            return new CategorySharedParameterCreateResult(created, skipped.Count, skipped);
        }
        finally
        {
            RestoreSharedParameterPath(app, oldSharedParameterPath);
        }
    }

    public static int MergeCreatedDefinitionsIntoParameterMap(
        string path,
        Autodesk.Revit.ApplicationServices.Application app)
    {
        string oldSharedParameterPath = app.SharedParametersFilename;

        try
        {
            DefinitionFile file = OpenOrRecreateSharedParameterFile(app, path, new List<string>());
            var map = ParameterMapService.LoadOrDefault();
            int merged = 0;

            foreach (DefinitionGroup group in file.Groups)
            {
                foreach (Definition definition in group.Definitions)
                {
                    if (definition is not ExternalDefinition external)
                        continue;

                    string key = MakeParameterKey(group.Name, external.Name);
                    string guid = external.GUID.ToString();

                    if (map.Parameters.TryGetValue(key, out var existing) &&
                        string.Equals(existing.Guid, guid, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    map.Parameters[key] = new ParameterSpec
                    {
                        Guid = guid,
                        RevitName = external.Name,
                        Notes = $"Generated from Revit category group {group.Name}."
                    };
                    merged++;
                }
            }

            string mapPath = ParameterMapService.GetExpectedMapPath();
            Directory.CreateDirectory(Path.GetDirectoryName(mapPath) ?? ".");
            File.WriteAllText(mapPath, JsonSerializer.Serialize(map, new JsonSerializerOptions { WriteIndented = true }));
            ParameterMapService.ResetCache();
            return merged;
        }
        finally
        {
            RestoreSharedParameterPath(app, oldSharedParameterPath);
        }
    }

    private static void CollectFromElement(
        IDictionary<string, CategorySharedParameterCandidate> rows,
        string category,
        Element element,
        string scope)
    {
        foreach (Parameter parameter in element.Parameters)
        {
            string rawName = parameter.Definition?.Name ?? string.Empty;
            if (ShouldSkip(rawName))
                continue;

            string normalizedName = NormalizeParameterName(category, rawName);
            if (ShouldSkip(normalizedName))
                continue;

            ForgeTypeId dataType = GetDataType(parameter);
            if (!IsCreatableSharedParameterDataType(dataType, out _))
                continue;

            string key = $"{category}|{normalizedName}";
            if (rows.ContainsKey(key))
                continue;

            rows[key] = new CategorySharedParameterCandidate
            {
                GroupName = category,
                ParameterName = normalizedName,
                OriginalName = rawName,
                DataType = dataType,
                Description = $"Amentum generated {scope} shared parameter. Source Revit parameter: {rawName}."
            };
        }
    }

    private static ForgeTypeId GetDataType(Parameter parameter)
    {
        try
        {
            ForgeTypeId id = parameter.Definition.GetDataType();
            if (!string.IsNullOrWhiteSpace(id.TypeId))
                return id;
        }
        catch
        {
        }

        return SpecTypeId.String.Text;
    }

    private static DefinitionFile OpenOrRecreateSharedParameterFile(
        Autodesk.Revit.ApplicationServices.Application app,
        string path,
        IList<string> skipped)
    {
        EnsureSharedParameterFile(path);

        try
        {
            return AssignAndOpenSharedParameterFile(app, path);
        }
        catch (Exception ex) when (ShouldRecreateSharedParameterFile(ex))
        {
            string backupPath = BackupBadSharedParameterFile(path);
            if (!string.IsNullOrWhiteSpace(backupPath))
                skipped.Add($"Existing shared parameter file could not be read by Revit and was backed up to {backupPath}.");
            else
                skipped.Add("Existing shared parameter file could not be read by Revit and was regenerated.");

            EnsureSharedParameterFile(path, force: true);
            return AssignAndOpenSharedParameterFile(app, path);
        }
    }

    private static DefinitionFile AssignAndOpenSharedParameterFile(
        Autodesk.Revit.ApplicationServices.Application app,
        string path)
    {
        app.SharedParametersFilename = path;
        return app.OpenSharedParameterFile()
            ?? throw new InvalidOperationException("Revit could not open the generated shared parameter file.");
    }

    private static void RestoreSharedParameterPath(
        Autodesk.Revit.ApplicationServices.Application app,
        string oldSharedParameterPath)
    {
        try
        {
            app.SharedParametersFilename = oldSharedParameterPath;
        }
        catch
        {
        }
    }

    private static bool ShouldRecreateSharedParameterFile(Exception ex)
    {
        string message = ex.Message ?? string.Empty;
        return message.IndexOf("readParamDatabase", StringComparison.OrdinalIgnoreCase) >= 0
            || message.IndexOf("shared parameter", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static string BackupBadSharedParameterFile(string path)
    {
        if (!File.Exists(path))
            return string.Empty;

        string backupPath = path + "." + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".bak";
        File.Copy(path, backupPath, overwrite: true);
        return backupPath;
    }

    private static bool IsCreatableSharedParameterDataType(ForgeTypeId dataType, out string reason)
    {
        string typeId = dataType.TypeId ?? string.Empty;
        string normalized = typeId.Trim().ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(normalized))
        {
            reason = "blank data type";
            return false;
        }

        if (normalized.StartsWith("autodesk.revit.category.", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("category")
            || normalized.Contains("familytype")
            || normalized.Contains("family_type"))
        {
            reason = "family type/category parameters are omitted";
            return false;
        }

        if (TryRevitDataTypeValidity(dataType, out bool valid) && !valid)
        {
            reason = "Revit reports this data type is not valid for a shared parameter";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    private static bool TryRevitDataTypeValidity(ForgeTypeId dataType, out bool valid)
    {
        valid = true;
        Type[] args = { typeof(ForgeTypeId) };

        foreach (Type owner in new[] { typeof(ExternalDefinitionCreationOptions), typeof(Parameter) })
        {
            try
            {
                MethodInfo? method = owner.GetMethod(
                    "IsValidDataType",
                    BindingFlags.Public | BindingFlags.Static,
                    binder: null,
                    types: args,
                    modifiers: null);
                if (method?.Invoke(null, new object[] { dataType }) is bool result)
                {
                    valid = result;
                    return true;
                }
            }
            catch
            {
            }
        }

        return false;
    }

    private static string DataTypeLabel(ForgeTypeId dataType)
    {
        try { return dataType.TypeId ?? string.Empty; }
        catch { return string.Empty; }
    }

    private static string NormalizeParameterName(string category, string name)
    {
        string clean = Regex.Replace(name.Trim(), @"\s+", " ");
        string lower = clean.ToLowerInvariant();
        string categoryLower = category.ToLowerInvariant();

        if (categoryLower.Contains("electrical"))
        {
            if (lower.Contains("potential") || lower is "voltage" or "volt")
                return "Voltage";
            if (lower.Contains("current"))
                return "Current";
            if (lower.Contains("apparent load") || lower.Contains("load va"))
                return "Apparent Load";
            if (lower.Contains("panel"))
                return "Panel";
            if (lower.Contains("circuit"))
                return "Circuit Number";
        }

        if (lower.Contains("flow"))
            return "Flow";
        if (lower.Contains("pressure"))
            return "Pressure";
        if (lower.Contains("temperature"))
            return "Temperature";
        if (lower.Contains("length"))
            return "Length";

        return clean;
    }

    private static string MakeParameterKey(string groupName, string parameterName)
    {
        string key = Regex.Replace(groupName + "_" + parameterName, @"[^A-Za-z0-9]+", "_").Trim('_');
        return string.IsNullOrWhiteSpace(key) ? parameterName : key;
    }

    private static string CleanGroupName(string? value)
    {
        string clean = Regex.Replace(value ?? string.Empty, @"\s+", " ").Trim();
        foreach (char ch in Path.GetInvalidFileNameChars())
            clean = clean.Replace(ch, '_');
        return string.IsNullOrWhiteSpace(clean) ? "General" : clean;
    }

    private static bool ShouldSkip(string value)
    {
        string text = (value ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(text))
            return true;
        if (text.StartsWith("<", StringComparison.Ordinal) && text.EndsWith(">", StringComparison.Ordinal))
            return true;

        string lower = text.ToLowerInvariant();
        if (lower is "family" or "type" or "category" or "family and type" or "family type")
            return true;

        return lower.Contains("family type") || lower.Contains("family and type");
    }

    private static DefinitionGroup GetOrCreateGroup(DefinitionFile file, string name)
    {
        foreach (DefinitionGroup group in file.Groups)
        {
            if (string.Equals(group.Name, name, StringComparison.OrdinalIgnoreCase))
                return group;
        }

        return file.Groups.Create(name);
    }

    private static bool HasDefinition(DefinitionGroup group, string name)
    {
        foreach (Definition definition in group.Definitions)
        {
            if (string.Equals(definition.Name, name, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static void EnsureSharedParameterFile(string path, bool force = false)
    {
        string folder = Path.GetDirectoryName(path) ?? ".";
        Directory.CreateDirectory(folder);
        if (!force && File.Exists(path) && new FileInfo(path).Length > 0 && LooksLikeSharedParameterFile(path))
            return;

        File.WriteAllText(
            path,
            "# This is a Revit shared parameter file.\r\n" +
            "# Generated by Amentum Revit.\r\n" +
            "*META\tVERSION\tMINVERSION\r\n" +
            "META\t2\t1\r\n" +
            "*GROUP\tID\tNAME\r\n" +
            "*PARAM\tGUID\tNAME\tDATATYPE\tDATACATEGORY\tGROUP\tVISIBLE\tDESCRIPTION\tUSERMODIFIABLE\tHIDEWHENNOVALUE\r\n",
            Encoding.Unicode);
    }

    private static bool LooksLikeSharedParameterFile(string path)
    {
        try
        {
            string text = File.ReadAllText(path);
            return text.Contains("*META\tVERSION\tMINVERSION")
                && text.Contains("*GROUP\tID\tNAME")
                && text.Contains("*PARAM\tGUID\tNAME\tDATATYPE");
        }
        catch
        {
            return false;
        }
    }
}

public sealed record CategorySharedParameterCreateResult(
    int Created,
    int Skipped,
    IReadOnlyList<string> SkippedMessages);

public sealed class CategorySharedParameterCandidate
{
    public string GroupName { get; init; } = "General";
    public string ParameterName { get; init; } = string.Empty;
    public string OriginalName { get; init; } = string.Empty;
    public ForgeTypeId DataType { get; init; } = SpecTypeId.String.Text;
    public string Description { get; init; } = string.Empty;
}
