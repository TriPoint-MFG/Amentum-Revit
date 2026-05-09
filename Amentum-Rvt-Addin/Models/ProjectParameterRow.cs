using System.Windows.Media;

namespace AmentumRevit.Models;

public sealed class ProjectParameterRow
{
    public ImageSource? Preview { get; init; }
    public string Category { get; init; } = string.Empty;
    public string Family { get; init; } = string.Empty;
    public string TypeName { get; init; } = string.Empty;
    public string ParameterName { get; init; } = string.Empty;
    public string Scope { get; init; } = string.Empty;
    public string StorageType { get; init; } = string.Empty;
    public string IsShared { get; init; } = string.Empty;
    public string Path { get; init; } = string.Empty;
}
