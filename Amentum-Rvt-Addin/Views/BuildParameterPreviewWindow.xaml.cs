using System.Text.Encodings.Web;
using System.Text.Json;
using System.Windows;
using AmentumRevit.Services;
using Microsoft.Web.WebView2.Core;

namespace AmentumRevit.Views;

public partial class BuildParameterPreviewWindow : Window
{
    private readonly IReadOnlyList<CategorySharedParameterCandidate> _candidates;

    public IReadOnlyList<CategorySharedParameterCandidate> SelectedCandidates { get; private set; } =
        Array.Empty<CategorySharedParameterCandidate>();

    public bool AddToProjectParameters { get; private set; }

    public BuildParameterPreviewWindow(IReadOnlyList<CategorySharedParameterCandidate> candidates)
    {
        InitializeComponent();
        _candidates = candidates;
        StatusLabel.Text = $"{candidates.Count} parameter candidate(s)";
        Loaded += BuildParameterPreviewWindow_Loaded;
    }

    private async void BuildParameterPreviewWindow_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            await PreviewWebView.EnsureCoreWebView2Async();
            PreviewWebView.CoreWebView2.WebMessageReceived += WebMessageReceived;
            PreviewWebView.NavigateToString(BuildHtml());
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                "WebView2 could not be started. Install the Microsoft Edge WebView2 Runtime and try again.\n\n" + ex.Message,
                "Amentum - Build Parameters",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            DialogResult = false;
            Close();
        }
    }

    private void WebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        var message = JsonSerializer.Deserialize<PreviewMessage>(e.WebMessageAsJson);
        if (message is null)
            return;

        if (string.Equals(message.Action, "cancel", StringComparison.OrdinalIgnoreCase))
        {
            DialogResult = false;
            Close();
            return;
        }

        if (!string.Equals(message.Action, "submit", StringComparison.OrdinalIgnoreCase))
            return;

        var selected = new List<CategorySharedParameterCandidate>();
        foreach (int index in message.Indexes.Distinct().OrderBy(i => i))
        {
            if (index >= 0 && index < _candidates.Count)
                selected.Add(_candidates[index]);
        }

        if (selected.Count == 0)
        {
            MessageBox.Show(
                "Select at least one parameter.",
                "Amentum - Build Parameters",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        SelectedCandidates = selected;
        AddToProjectParameters = message.AddToProjectParameters;
        DialogResult = true;
        Close();
    }

    private string BuildHtml()
    {
        var rows = _candidates.Select((c, index) => new
        {
            index,
            group = c.GroupName,
            name = c.ParameterName,
            original = c.OriginalName,
            dataType = c.DataType.TypeId,
            description = c.Description
        }).ToList();

        var json = JsonSerializer.Serialize(rows, new JsonSerializerOptions
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        });

        return """
<!doctype html>
<html>
<head>
<meta charset="utf-8">
<style>
  :root { --navy:#003865; --orange:#E87722; --line:#d7dee8; --soft:#f5f7fa; --text:#1f2937; }
  * { box-sizing:border-box; }
  body { margin:0; font-family:"Segoe UI", Arial, sans-serif; color:var(--text); background:white; }
  .toolbar { position:sticky; top:0; z-index:2; display:flex; align-items:center; gap:8px; padding:10px 12px; background:var(--soft); border-bottom:1px solid var(--line); }
  .toolbar input[type=search] { width:320px; padding:8px 10px; border:1px solid var(--line); border-radius:4px; }
  button { border:0; border-radius:4px; padding:8px 12px; background:var(--navy); color:white; font-weight:600; cursor:pointer; }
  button.secondary { background:#5b6770; }
  button.orange { background:var(--orange); }
  label.option { display:flex; align-items:center; gap:6px; margin-left:auto; font-size:13px; }
  table { border-collapse:collapse; width:100%; table-layout:fixed; }
  th, td { border-bottom:1px solid var(--line); padding:8px 10px; text-align:left; vertical-align:top; font-size:13px; }
  th { position:sticky; top:53px; z-index:1; background:#fff; color:#344054; border-bottom:2px solid var(--line); }
  tr:hover { background:#fff7ed; }
  .check { width:42px; text-align:center; }
  .group { width:180px; }
  .name { width:190px; font-weight:600; }
  .original { width:190px; color:#4b5563; }
  .type { width:260px; color:#667085; font-family:Consolas, monospace; font-size:12px; overflow-wrap:anywhere; }
  .desc { color:#4b5563; }
  .count { margin-left:4px; font-size:13px; color:#4b5563; }
</style>
</head>
<body>
<div class="toolbar">
  <input id="filter" type="search" placeholder="Filter by category, parameter, original name..." oninput="render()">
  <button onclick="setAll(true)">Check All</button>
  <button class="secondary" onclick="setAll(false)">Clear</button>
  <span class="count" id="count"></span>
  <label class="option"><input id="bind" type="checkbox"> Add selected to Project Parameters</label>
  <button class="orange" onclick="submit(false)">Create File</button>
  <button class="orange" onclick="submit(true)">Create + Add</button>
</div>
<table>
  <thead>
    <tr>
      <th class="check"></th>
      <th class="group">Category</th>
      <th class="name">Shared Parameter</th>
      <th class="original">Source Parameter</th>
      <th class="type">Data Type</th>
      <th class="desc">Description</th>
    </tr>
  </thead>
  <tbody id="rows"></tbody>
</table>
<script>
const rows = __ROWS__;
const checked = new Set(rows.map(r => r.index));

function visibleRows() {
  const q = document.getElementById('filter').value.trim().toLowerCase();
  if (!q) return rows;
  return rows.filter(r => [r.group,r.name,r.original,r.dataType,r.description].some(v => String(v||'').toLowerCase().includes(q)));
}
function escapeHtml(v) {
  return String(v ?? '').replace(/[&<>"']/g, s => ({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[s]));
}
function render() {
  const body = document.getElementById('rows');
  const visible = visibleRows();
  body.innerHTML = visible.map(r => `
    <tr>
      <td class="check"><input type="checkbox" ${checked.has(r.index) ? 'checked' : ''} onchange="toggle(${r.index}, this.checked)"></td>
      <td class="group">${escapeHtml(r.group)}</td>
      <td class="name">${escapeHtml(r.name)}</td>
      <td class="original">${escapeHtml(r.original)}</td>
      <td class="type">${escapeHtml(r.dataType)}</td>
      <td class="desc">${escapeHtml(r.description)}</td>
    </tr>`).join('');
  document.getElementById('count').textContent = `${checked.size} selected / ${rows.length} total`;
}
function toggle(i, value) {
  if (value) checked.add(i); else checked.delete(i);
  render();
}
function setAll(value) {
  const visible = visibleRows();
  for (const row of visible) {
    if (value) checked.add(row.index); else checked.delete(row.index);
  }
  render();
}
function submit(forceBind) {
  const addToProjectParameters = forceBind || document.getElementById('bind').checked;
  chrome.webview.postMessage({ action:'submit', indexes:Array.from(checked), addToProjectParameters });
}
render();
</script>
</body>
</html>
""".Replace("__ROWS__", json);
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private sealed class PreviewMessage
    {
        public string Action { get; init; } = string.Empty;
        public bool AddToProjectParameters { get; init; }
        public List<int> Indexes { get; init; } = new();
    }
}
