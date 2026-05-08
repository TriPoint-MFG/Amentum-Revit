using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace AmentumRevit.Views;

/// <summary>
/// View-model for a single row in the Sync Panel DataGrid.
/// Represents one (Element × Parameter) cell the user can edit.
/// </summary>
public class ParameterEditRow : INotifyPropertyChanged
{
    // ── Identity (read-only columns) ──────────────────────────────────────────

    public string UniqueId   { get; init; } = string.Empty;
    public long   ElementId  { get; init; }
    public string Category   { get; init; } = string.Empty;
    public string Name       { get; init; } = string.Empty;
    public string ParamKey   { get; init; } = string.Empty;
    public string CurrentValue { get; init; } = string.Empty;

    // ── Editable column ───────────────────────────────────────────────────────

    private string _newValue = string.Empty;
    public string NewValue
    {
        get => _newValue;
        set
        {
            if (_newValue == value) return;
            _newValue = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsDirty));
        }
    }

    /// <summary>True when the user has typed a value different from the original.</summary>
    public bool IsDirty => NewValue != CurrentValue;

    // ── INotifyPropertyChanged ────────────────────────────────────────────────

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>
/// Lightweight record returned to SyncElementsCommand after the window closes.
/// </summary>
public record PendingChange(string UniqueId, string ParamKey, string? NewValue);
