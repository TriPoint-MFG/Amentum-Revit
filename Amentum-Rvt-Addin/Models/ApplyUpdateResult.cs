namespace AmentumRevit.Models;

public sealed record ApplyUpdateResult(
    ExcelParameterUpdate Update,
    bool Succeeded,
    string? Error
);
