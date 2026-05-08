using System.Globalization;
using Autodesk.Revit.DB;
using AmentumRevit.Models;

namespace AmentumRevit.Services;

public static class RevitParameterUtil
{
    public static Parameter? GetParameter(Element element, Guid guid, string? fallbackName = null)
    {
        Parameter? p = null;
        try { p = element.get_Parameter(guid); } catch { /* ignore */ }

        if (p is null && !string.IsNullOrWhiteSpace(fallbackName))
            p = element.LookupParameter(fallbackName);

        return p;
    }

    public static Parameter? GetParameter(Element element, ParameterSpec spec)
    {
        if (!string.IsNullOrWhiteSpace(spec.Guid) && Guid.TryParse(spec.Guid, out var g))
            return GetParameter(element, g, spec.RevitName);

        if (!string.IsNullOrWhiteSpace(spec.RevitName))
            return element.LookupParameter(spec.RevitName);

        return null;
    }

    public static string? GetParameterValueAsString(Element element, ParameterSpec spec)
    {
        var p = GetParameter(element, spec);
        if (p is null) return null;

        string? value = null;
        try { value = p.AsValueString(); } catch { /* ignore */ }
        if (string.IsNullOrWhiteSpace(value))
        {
            try { value = p.AsString(); } catch { /* ignore */ }
        }

        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    public static bool TrySetParameterFromString(Element element, ParameterSpec spec,
        string? value, out string? error)
    {
        var p = GetParameter(element, spec);
        if (p is null)
        {
            error = "Parameter not found on element";
            return false;
        }
        return TrySet(p, value, out error);
    }

    public static bool TrySetParameterFromString(Element element, string parameterName,
        string? value, out string? error)
    {
        error = null;
        if (string.IsNullOrWhiteSpace(parameterName))
        {
            error = "Parameter name is empty";
            return false;
        }

        var p = element.LookupParameter(parameterName);
        if (p is null)
        {
            error = "Parameter not found on element";
            return false;
        }
        return TrySet(p, value, out error);
    }

    private static bool TrySet(Parameter p, string? value, out string? error)
    {
        error = null;
        if (p.IsReadOnly) { error = "Parameter is read-only"; return false; }

        try
        {
            switch (p.StorageType)
            {
                case StorageType.String:
                    p.Set(value ?? string.Empty);
                    return true;

                case StorageType.Integer:
                    if (string.IsNullOrWhiteSpace(value)) { p.Set(0); return true; }
                    if (bool.TryParse(value, out bool b)) { p.Set(b ? 1 : 0); return true; }
                    if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int i))
                    { error = $"Cannot parse integer '{value}'"; return false; }
                    p.Set(i);
                    return true;

                case StorageType.Double:
                    if (string.IsNullOrWhiteSpace(value)) { p.Set(0.0); return true; }
                    if (!double.TryParse(value,
                        NumberStyles.Float | NumberStyles.AllowThousands,
                        CultureInfo.InvariantCulture, out double d))
                    { error = $"Cannot parse double '{value}'. Use '.' as decimal separator."; return false; }
                    // NOTE: Revit stores lengths in internal units (feet).
                    // For production, apply UnitUtils.ConvertToInternalUnits() based on p.Definition.GetDataType().
                    p.Set(d);
                    return true;

                case StorageType.ElementId:
                    if (string.IsNullOrWhiteSpace(value)) { p.Set(ElementId.InvalidElementId); return true; }
                    if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int idInt))
                    { error = $"Cannot parse ElementId '{value}'"; return false; }
                    p.Set(new ElementId(idInt));
                    return true;

                default:
                    error = "Unsupported parameter storage type";
                    return false;
            }
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }
}
