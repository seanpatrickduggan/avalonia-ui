using CommunityToolkit.Mvvm.ComponentModel;
using FileProcessor.Core.Logging;
using System.Collections.ObjectModel;
using System.Text.Json;
using System.IO;
using System;
using System.Linq;
using System.Collections.Generic;
using Avalonia.Threading;

namespace LogViewer.Shared.ViewModels;

public partial class SharedLogViewerViewModel : ObservableObject
{
    [ObservableProperty] private string _logFilePath = string.Empty;
    [ObservableProperty] private ObservableCollection<ItemLogEntry> _entries = new();
    [ObservableProperty] private string _filterText = string.Empty;
    [ObservableProperty] private bool _tail = true;
    [ObservableProperty] private bool _showTrace = true;
    [ObservableProperty] private bool _showDebug = true;
    [ObservableProperty] private bool _showInfo = true;
    [ObservableProperty] private bool _showWarning = true;
    [ObservableProperty] private bool _showError = true;
    [ObservableProperty] private bool _showCritical = true;
    [ObservableProperty] private ObservableCollection<string> _categories = new();
    [ObservableProperty] private string? _selectedCategory;
    [ObservableProperty] private ObservableCollection<string> _subcategories = new();
    [ObservableProperty] private string? _selectedSubcategory;
    [ObservableProperty] private int _totalCount;
    [ObservableProperty] private int _filteredCount;

    private readonly List<ItemLogEntry> _all = new();
    private long _lastLength;
    private readonly DispatcherTimer _timer;

    public SharedLogViewerViewModel()
    {
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(700) };
        _timer.Tick += (_, _) => TailUpdate();
        _timer.Start();
    }

    partial void OnLogFilePathChanged(string value)
    {
        if (!string.IsNullOrWhiteSpace(value)) LoadInitial();
    }

    partial void OnFilterTextChanged(string value) => ApplyFilters();
    partial void OnSelectedCategoryChanged(string? value) { UpdateSubcategoryList(); ApplyFilters(); }
    partial void OnSelectedSubcategoryChanged(string? value) => ApplyFilters();
    partial void OnShowTraceChanged(bool value) => ApplyFilters();
    partial void OnShowDebugChanged(bool value) => ApplyFilters();
    partial void OnShowInfoChanged(bool value) => ApplyFilters();
    partial void OnShowWarningChanged(bool value) => ApplyFilters();
    partial void OnShowErrorChanged(bool value) => ApplyFilters();
    partial void OnShowCriticalChanged(bool value) => ApplyFilters();

    public void Initialize(string path)
    {
        LogFilePath = path;
        LoadInitial();
    }

    private void LoadInitial()
    {
        _all.Clear();
        _lastLength = 0;
        if (!File.Exists(LogFilePath)) return;
        using var fs = new FileStream(LogFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(fs);
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            ParseAndAdd(line);
        }
        _lastLength = fs.Length;
        RebuildCategoryLists();
        ApplyFilters();
    }

    private void TailUpdate()
    {
        if (!Tail) return;
        if (!File.Exists(LogFilePath)) return;
        var info = new FileInfo(LogFilePath);
        if (info.Length == _lastLength) return;
        using var fs = new FileStream(LogFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        fs.Seek(_lastLength, SeekOrigin.Begin);
        using var reader = new StreamReader(fs);
        string? line; var added = false;
        while ((line = reader.ReadLine()) != null)
        {
            if (ParseAndAdd(line)) added = true;
        }
        _lastLength = fs.Length;
        if (added)
        {
            RebuildCategoryLists();
            ApplyFilters();
        }
    }

    private bool ParseAndAdd(string line)
    {
        try
        {
            using var doc = JsonDocument.Parse(line);
            var root = doc.RootElement;
            var ts = root.TryGetProperty("Timestamp", out var tEl) && tEl.ValueKind == JsonValueKind.String ? tEl.GetDateTime() : DateTime.UtcNow;
            var msg = root.TryGetProperty("RenderedMessage", out var rmEl) ? rmEl.GetString() ?? string.Empty : root.TryGetProperty("MessageTemplate", out var mtEl) ? mtEl.GetString() ?? string.Empty : string.Empty;
            var levelStr = root.TryGetProperty("Level", out var lvlEl) ? lvlEl.GetString() ?? "Information" : "Information";
            LogSeverity severity = levelStr switch { "Verbose" => LogSeverity.Trace, "Debug" => LogSeverity.Debug, "Information" => LogSeverity.Info, "Warning" => LogSeverity.Warning, "Error" => LogSeverity.Error, "Fatal" => LogSeverity.Critical, _ => LogSeverity.Info };
            string cat = string.Empty; string sub = string.Empty; object? dataObj = null;
            if (root.TryGetProperty("Properties", out var props))
            {
                if (props.TryGetProperty("cat", out var catEl)) cat = catEl.ToString();
                if (props.TryGetProperty("sub", out var subEl)) sub = subEl.ToString();
                if (props.TryGetProperty("severityRank", out var rankEl))
                {
                    var rank = rankEl.GetInt32();
                    if (rank >= 0 && rank <= 5) severity = (LogSeverity)rank;
                }
            }
            var entry = new ItemLogEntry(ts, severity, cat, sub, msg, dataObj);
            _all.Add(entry);
            return true;
        }
        catch { return false; }
    }

    private void RebuildCategoryLists()
    {
        var cats = _all.Select(a => a.Category).Where(c => !string.IsNullOrEmpty(c)).Distinct().OrderBy(c => c).ToList();
        Categories.Clear();
        foreach (var c in cats) Categories.Add(c);
        if (SelectedCategory != null && !cats.Contains(SelectedCategory)) SelectedCategory = null;
        UpdateSubcategoryList();
    }

    private void UpdateSubcategoryList()
    {
        Subcategories.Clear();
        if (SelectedCategory == null) return;
        var subs = _all.Where(a => a.Category == SelectedCategory).Select(a => a.Subcategory).Where(s => !string.IsNullOrEmpty(s)).Distinct().OrderBy(s => s);
        foreach (var s in subs) Subcategories.Add(s);
        if (SelectedSubcategory != null && !Subcategories.Contains(SelectedSubcategory)) SelectedSubcategory = null;
    }

    private bool LevelVisible(LogSeverity sev) => sev switch
    {
        LogSeverity.Trace => ShowTrace,
        LogSeverity.Debug => ShowDebug,
        LogSeverity.Info => ShowInfo,
        LogSeverity.Warning => ShowWarning,
        LogSeverity.Error => ShowError,
        LogSeverity.Critical => ShowCritical,
        _ => true
    };

    private void ApplyFilters()
    {
        var text = FilterText?.Trim();
        var list = _all.Where(e => LevelVisible(e.Level));
        if (!string.IsNullOrEmpty(SelectedCategory)) list = list.Where(e => e.Category == SelectedCategory);
        if (!string.IsNullOrEmpty(SelectedSubcategory)) list = list.Where(e => e.Subcategory == SelectedSubcategory);
        if (!string.IsNullOrEmpty(text)) list = list.Where(e => (e.Message?.IndexOf(text, StringComparison.OrdinalIgnoreCase) ?? -1) >= 0);
        var filtered = list.ToList();
        Entries.Clear();
        foreach (var e in filtered) Entries.Add(e);
        TotalCount = _all.Count;
        FilteredCount = filtered.Count;
    }
}
