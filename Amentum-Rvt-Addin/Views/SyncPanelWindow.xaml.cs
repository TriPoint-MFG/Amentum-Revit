using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Data;
using AmentumRevit.Models;
using AmentumRevit.Services;
using ClosedXML.Excel;
using Microsoft.Win32;

namespace AmentumRevit.Views;

/// <summary>
/// Code-behind for the Amentum Sync Panel.
///
/// Lifecycle:
///   1. Constructor receives pre-built ElementPayload list + ParameterMap.
///   2. All payload keys become rows (Element × Parameter) in the DataGrid.
///   3. User edits the "New Value" column.
///   4. On Apply → DialogResult = true; caller reads PendingChanges.
///   5. Transaction happens in SyncElementsCommand.Execute() (never here).
/// </summary>
public partial class SyncPanelWindow : Window
{
    // ── Public output ─────────────────────────────────────────────────────────

    /// <summary>Populated when the user clicks Apply.  Caller commits the transaction.</summary>
    public IReadOnlyList<PendingChange> PendingChanges { get; private set; } =
        Array.Empty<PendingChange>();

    // ── Private state ─────────────────────────────────────────────────────────

    private readonly ObservableCollection<ParameterEditRow> _allRows = new();
    private readonly ICollectionView                        _view;
    private readonly ParameterMap                           _map;

    // ── Constructor ───────────────────────────────────────────────────────────

    public SyncPanelWindow(IList<ElementPayload> payloads, ParameterMap map)
    {
        InitializeComponent();
        _map = map;

        // Build one row per (element × parameter key)
        var exportKeys = map.ExcelExportKeys ?? map.Parameters.Keys.ToList();

        foreach (var payload in payloads)
        {
            foreach (var key in exportKeys)
            {
                string current = payload.Parameters.GetValueOrDefault(key) ?? string.Empty;
                _allRows.Add(new ParameterEditRow
                {
                    UniqueId     = payload.UniqueId,
                    ElementId    = payload.ElementId,
                    Category     = payload.Category  ?? string.Empty,
                    Name         = payload.Name      ?? string.Empty,
                    ParamKey     = key,
                    CurrentValue = current,
                    NewValue     = current   // starts equal — not dirty
                });
            }
        }

        // Wire collection view for filtering
        _view = CollectionViewSource.GetDefaultView(_allRows);
        _view.Filter = ApplyFilter;
        MainGrid.ItemsSource = _view;

        // Header counters
        ElementCountLabel.Text =
            $"  ·  {payloads.Count} element(s)   {_allRows.Count} row(s)";

        UpdateDirtyCount();

        // Track dirty changes to update the counter live
        foreach (var row in _allRows)
            row.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(ParameterEditRow.IsDirty))
                    UpdateDirtyCount();
            };
    }

    // ── Filtering ─────────────────────────────────────────────────────────────

    private bool ApplyFilter(object obj)
    {
        if (obj is not ParameterEditRow row) return false;

        if (ShowDirtyOnly.IsChecked == true && !row.IsDirty)
            return false;

        string text = FilterBox.Text.Trim();
        if (string.IsNullOrEmpty(text)) return true;

        return row.Category.Contains(text, StringComparison.OrdinalIgnoreCase)
            || row.Name.Contains(text, StringComparison.OrdinalIgnoreCase)
            || row.ParamKey.Contains(text, StringComparison.OrdinalIgnoreCase)
            || row.CurrentValue.Contains(text, StringComparison.OrdinalIgnoreCase);
    }

    private void FilterBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        _view.Refresh();
        UpdateStatus();
    }

    private void DirtyFilter_Changed(object sender, RoutedEventArgs e)
    {
        _view.Refresh();
        UpdateStatus();
    }

    // ── Status helpers ────────────────────────────────────────────────────────

    private void UpdateDirtyCount()
    {
        int dirtyCount = _allRows.Count(r => r.IsDirty);
        DirtyCountLabel.Text = dirtyCount > 0
            ? $"{dirtyCount} pending change(s)"
            : "No changes";
        ApplyBtn.IsEnabled = dirtyCount > 0;
    }

    private void UpdateStatus()
    {
        int visible = _view.Cast<object>().Count();
        StatusLabel.Text = $"{visible} / {_allRows.Count} row(s) shown";
    }

    // ── Button handlers ───────────────────────────────────────────────────────

    private void ApplyBtn_Click(object sender, RoutedEventArgs e)
    {
        // Commit any in-progress cell edit before reading values
        MainGrid.CommitEdit();

        PendingChanges = _allRows
            .Where(r => r.IsDirty)
            .Select(r => new PendingChange(r.UniqueId, r.ParamKey, r.NewValue))
            .ToList();

        DialogResult = true;
        Close();
    }

    private void CancelBtn_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void ClearBtn_Click(object sender, RoutedEventArgs e)
    {
        foreach (var row in _allRows)
            row.NewValue = row.CurrentValue;
    }

    private void ExportBtn_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new SaveFileDialog
        {
            Title      = "Amentum — Export Sync View to Excel",
            Filter     = "Excel Workbook (*.xlsx)|*.xlsx",
            FileName   = $"Amentum_SyncView_{DateTime.Now:yyyyMMdd_HHmm}.xlsx",
            DefaultExt = "xlsx",
            OverwritePrompt = true
        };

        if (dlg.ShowDialog() != true) return;

        try
        {
            ExportViewToExcel(dlg.FileName);
            MessageBox.Show(
                $"Exported to:\n{dlg.FileName}",
                "Amentum — Export",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Export failed:\n{ex.Message}",
                "Amentum — Export Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void ExportViewToExcel(string path)
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("SyncView");

        ws.Cell(1, 1).Value = "Category";
        ws.Cell(1, 2).Value = "Name";
        ws.Cell(1, 3).Value = "ElementId";
        ws.Cell(1, 4).Value = "Parameter";
        ws.Cell(1, 5).Value = "CurrentValue";
        ws.Cell(1, 6).Value = "NewValue";
        ws.Cell(1, 7).Value = "IsDirty";

        var headerRow = ws.Row(1);
        headerRow.Style.Font.Bold = true;
        headerRow.Style.Fill.BackgroundColor = XLColor.FromHtml("#003865");
        headerRow.Style.Font.FontColor = XLColor.White;

        int r = 2;
        foreach (var row in _view.Cast<ParameterEditRow>())
        {
            ws.Cell(r, 1).Value = row.Category;
            ws.Cell(r, 2).Value = row.Name;
            ws.Cell(r, 3).Value = row.ElementId;
            ws.Cell(r, 4).Value = row.ParamKey;
            ws.Cell(r, 5).Value = row.CurrentValue;
            ws.Cell(r, 6).Value = row.NewValue;
            ws.Cell(r, 7).Value = row.IsDirty ? "Yes" : string.Empty;
            r++;
        }

        ws.Columns().AdjustToContents();
        ws.SheetView.FreezeRows(1);
        wb.SaveAs(path);
    }
}
