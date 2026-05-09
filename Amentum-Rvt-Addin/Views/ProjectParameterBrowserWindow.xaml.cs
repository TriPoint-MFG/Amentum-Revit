using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Data;
using AmentumRevit.Models;

namespace AmentumRevit.Views;

public partial class ProjectParameterBrowserWindow : Window
{
    private readonly ObservableCollection<ProjectParameterRow> _rows;
    private readonly ICollectionView _view;

    public ProjectParameterBrowserWindow(IReadOnlyList<ProjectParameterRow> rows)
    {
        InitializeComponent();
        _rows = new ObservableCollection<ProjectParameterRow>(rows);
        _view = CollectionViewSource.GetDefaultView(_rows);
        _view.Filter = ApplyFilter;
        MainGrid.ItemsSource = _view;
        CountLabel.Text = $"  ·  {_rows.Count} row(s)";
        UpdateStatus();
    }

    private bool ApplyFilter(object obj)
    {
        if (obj is not ProjectParameterRow row)
            return false;

        string text = FilterBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(text))
            return true;

        return row.Category.Contains(text, StringComparison.OrdinalIgnoreCase)
            || row.Family.Contains(text, StringComparison.OrdinalIgnoreCase)
            || row.TypeName.Contains(text, StringComparison.OrdinalIgnoreCase)
            || row.ParameterName.Contains(text, StringComparison.OrdinalIgnoreCase)
            || row.Scope.Contains(text, StringComparison.OrdinalIgnoreCase)
            || row.Path.Contains(text, StringComparison.OrdinalIgnoreCase);
    }

    private void FilterBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        _view.Refresh();
        UpdateStatus();
    }

    private void UpdateStatus()
    {
        StatusLabel.Text = $"{_view.Cast<object>().Count()} / {_rows.Count} row(s) shown";
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
