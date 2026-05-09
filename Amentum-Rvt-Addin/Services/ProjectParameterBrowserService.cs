using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Media.Imaging;
using Autodesk.Revit.DB;
using AmentumRevit.Models;

namespace AmentumRevit.Services;

public static class ProjectParameterBrowserService
{
    public static List<ProjectParameterRow> Collect(Document doc)
    {
        var rows = new List<ProjectParameterRow>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        AddProjectBindings(doc, rows, seen);
        AddElementParameters(doc, rows, seen);

        return rows
            .OrderBy(r => r.Category, StringComparer.OrdinalIgnoreCase)
            .ThenBy(r => r.Family, StringComparer.OrdinalIgnoreCase)
            .ThenBy(r => r.TypeName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(r => r.ParameterName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static void AddProjectBindings(Document doc, List<ProjectParameterRow> rows, HashSet<string> seen)
    {
        DefinitionBindingMapIterator iterator = doc.ParameterBindings.ForwardIterator();
        iterator.Reset();

        while (iterator.MoveNext())
        {
            if (iterator.Key is not Definition definition)
                continue;

            string scope = iterator.Current is TypeBinding ? "Project Type" : "Project Instance";
            if (iterator.Current is not ElementBinding binding)
            {
                AddRow(rows, seen, new ProjectParameterRow
                {
                    Category = "(Project)",
                    ParameterName = definition.Name,
                    Scope = scope,
                    StorageType = DataTypeLabel(definition),
                    IsShared = definition is ExternalDefinition ? "Yes" : string.Empty,
                    Path = ModelPath(doc)
                });
                continue;
            }

            foreach (Category category in binding.Categories)
            {
                AddRow(rows, seen, new ProjectParameterRow
                {
                    Category = category.Name,
                    ParameterName = definition.Name,
                    Scope = scope,
                    StorageType = DataTypeLabel(definition),
                    IsShared = definition is ExternalDefinition ? "Yes" : string.Empty,
                    Path = ModelPath(doc)
                });
            }
        }
    }

    private static void AddElementParameters(Document doc, List<ProjectParameterRow> rows, HashSet<string> seen)
    {
        int maxElements = EnvironmentScanService.GetEnvInt("AMENTUM_PANEL_MAX_ELEMENTS", 2500);
        var elements = new FilteredElementCollector(doc)
            .WhereElementIsNotElementType()
            .Where(e => e?.Category is not null)
            .Take(maxElements)
            .ToList();
        var previewCache = new Dictionary<long, BitmapImage?>();

        foreach (Element element in elements)
        {
            AddParametersFromElement(doc, element, element, "Instance", rows, seen, previewCache);

            Element? typeElement = null;
            try
            {
                ElementId typeId = element.GetTypeId();
                if (typeId != ElementId.InvalidElementId)
                    typeElement = doc.GetElement(typeId);
            }
            catch
            {
            }

            if (typeElement is not null)
                AddParametersFromElement(doc, element, typeElement, "Type", rows, seen, previewCache);
        }
    }

    private static void AddParametersFromElement(
        Document doc,
        Element owner,
        Element parameterSource,
        string scope,
        List<ProjectParameterRow> rows,
        HashSet<string> seen,
        Dictionary<long, BitmapImage?> previewCache)
    {
        string category = owner.Category?.Name ?? string.Empty;
        string family = FamilyName(owner, doc);
        string type = TypeName(owner, doc);
        var preview = PreviewFor(owner, doc, previewCache);

        foreach (Parameter parameter in parameterSource.Parameters)
        {
            string name = parameter.Definition?.Name ?? string.Empty;
            if (string.IsNullOrWhiteSpace(name))
                continue;

            AddRow(rows, seen, new ProjectParameterRow
            {
                Preview = preview,
                Category = category,
                Family = family,
                TypeName = type,
                ParameterName = name,
                Scope = scope,
                StorageType = parameter.StorageType.ToString(),
                IsShared = SafeIsShared(parameter) ? "Yes" : string.Empty,
                Path = ModelPath(doc)
            });
        }
    }

    private static void AddRow(List<ProjectParameterRow> rows, HashSet<string> seen, ProjectParameterRow row)
    {
        string key = string.Join("|", row.Category, row.Family, row.TypeName, row.ParameterName, row.Scope);
        if (seen.Add(key))
            rows.Add(row);
    }

    private static string FamilyName(Element element, Document doc)
    {
        if (element is FamilyInstance fi)
            return fi.Symbol?.FamilyName ?? string.Empty;
        if (doc.GetElement(element.GetTypeId()) is FamilySymbol fs)
            return fs.FamilyName;
        return string.Empty;
    }

    private static string TypeName(Element element, Document doc)
    {
        if (element is FamilyInstance fi)
            return fi.Symbol?.Name ?? string.Empty;
        return doc.GetElement(element.GetTypeId())?.Name ?? string.Empty;
    }

    private static BitmapImage? PreviewFor(Element element, Document doc, Dictionary<long, BitmapImage?> previewCache)
    {
        long cacheKey = PreviewCacheKey(element);
        if (previewCache.TryGetValue(cacheKey, out var cached))
            return cached;

        try
        {
            ElementType? elementType = doc.GetElement(element.GetTypeId()) as ElementType
                ?? element as ElementType;
            if (elementType is null)
            {
                previewCache[cacheKey] = null;
                return null;
            }

            using Bitmap? bitmap = elementType.GetPreviewImage(new Size(64, 64));
            if (bitmap is null)
            {
                previewCache[cacheKey] = null;
                return null;
            }

            using var stream = new MemoryStream();
            bitmap.Save(stream, ImageFormat.Png);
            stream.Position = 0;

            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.StreamSource = stream;
            image.EndInit();
            image.Freeze();
            previewCache[cacheKey] = image;
            return image;
        }
        catch
        {
            previewCache[cacheKey] = null;
            return null;
        }
    }

    private static long PreviewCacheKey(Element element)
    {
        try
        {
            ElementId typeId = element.GetTypeId();
            if (typeId != ElementId.InvalidElementId)
                return typeId.Value;
        }
        catch
        {
        }

        return element.Id.Value;
    }

    private static bool SafeIsShared(Parameter parameter)
    {
        try { return parameter.IsShared; }
        catch { return false; }
    }

    private static string DataTypeLabel(Definition definition)
    {
        try { return definition.GetDataType().TypeId; }
        catch { return string.Empty; }
    }

    private static string ModelPath(Document doc)
    {
        if (!string.IsNullOrWhiteSpace(doc.PathName))
            return doc.PathName;
        return string.IsNullOrWhiteSpace(doc.Title) ? "(unsaved model)" : $"(unsaved) {doc.Title}";
    }
}
