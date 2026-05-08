namespace AmentumRevit.Models;

/// <summary>
/// A single (Element, Parameter, Value) update row read from the Excel "Updates" sheet.
/// </summary>
public sealed record ExcelParameterUpdate(
    string ElementUniqueId,
    string? ParameterKey,
    string? ParameterGuid,
    string Value
);
