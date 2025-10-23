using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FileProcessor.Core.Logging;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using Avalonia.Platform.Storage;
using Avalonia.Controls;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace LogViewer.UI;

public partial class MainWindowViewModel : ObservableObject, IDisposable
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

    // New metrics & state
    [ObservableProperty] private int _totalCount;
    [ObservableProperty] private int _filteredCount;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _lastError = string.Empty;

    private readonly List<ItemLogEntry> _all = new();
    private long _lastLen;
    private readonly Avalonia.Threading.DispatcherTimer _timer;
    private bool _disposed;

    public MainWindowViewModel()
    {
        _timer = new Avalonia.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(700) };
        _timer.Tick += (_, _) => TailUpdate();
        _timer.Start();
        if (string.IsNullOrWhiteSpace(LogFilePath))
        {
            var sample = Path.Combine(AppContext.BaseDirectory, "sample-log.jsonl");
            if (File.Exists(sample))
            {
                LogFilePath = sample; // triggers reload via partial method
            }
        }
    }

    partial void OnLogFilePathChanged(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        LoadInitial();
    }

    [RelayCommand]
    private async Task Browse()
    {
        if (App.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime life) return;
        if (life.MainWindow?.StorageProvider is null) return;
        var files = await life.MainWindow.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select log file",
            AllowMultiple = false
        });
        var f = files.FirstOrDefault();
        if (f != null)
        {
            LogFilePath = f.Path.LocalPath; // auto loads
        }
    }

    [RelayCommand]
    private void Reload()
    {
        if (!string.IsNullOrWhiteSpace(LogFilePath) && File.Exists(LogFilePath)) LoadInitial();
    }

    [RelayCommand]
    private void ClearFilters()
    {
        FilterText = string.Empty;
        SelectedCategory = null;
        SelectedSubcategory = null;
        ShowTrace = ShowDebug = ShowInfo = ShowWarning = ShowError = ShowCritical = true;
        ApplyFilters();
    }

    [RelayCommand]
    private async Task ExportFiltered()
    {
        try
        {
            if (App.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime life) return;
            if (life.MainWindow?.StorageProvider is null) return;
            var suggestions = new List<FilePickerFileType> { new("JSON Lines") { Patterns = new[] { "*.jsonl" } } };
            var file = await life.MainWindow.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Export filtered log",
                SuggestedFileName = "filtered-log.jsonl",
                FileTypeChoices = suggestions
            });
            if (file == null) return;
            using var stream = await file.OpenWriteAsync();
            using var writer = new StreamWriter(stream);
            foreach (var e in Entries)
            {
                var obj = new
                {
                    Timestamp = e.TsUtc.ToString("O"),
                    Level = e.Level.ToString(),
                    e.Category,
                    e.Subcategory,
                    e.Message
                };
                await writer.WriteLineAsync(JsonSerializer.Serialize(obj));
            }
            StatusMessage = $"Exported {Entries.Count} lines.";
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
        }
    }

    [RelayCommand]
    private void ToggleTail() => Tail = !Tail;

    partial void OnFilterTextChanged(string value) => ApplyFilters();
    partial void OnSelectedCategoryChanged(string? value) { UpdateSubcategoryList(); ApplyFilters(); }
    partial void OnSelectedSubcategoryChanged(string? value) => ApplyFilters();
    partial void OnShowTraceChanged(bool value) => ApplyFilters();
    partial void OnShowDebugChanged(bool value) => ApplyFilters();
    partial void OnShowInfoChanged(bool value) => ApplyFilters();
    partial void OnShowWarningChanged(bool value) => ApplyFilters();
    partial void OnShowErrorChanged(bool value) => ApplyFilters();
    partial void OnShowCriticalChanged(bool value) => ApplyFilters();

    private void LoadInitial()
    {
        try
        {
            IsBusy = true;
            _all.Clear();
            Entries.Clear();
            _lastLen = 0;
            if (string.IsNullOrWhiteSpace(LogFilePath) || !File.Exists(LogFilePath)) { StatusMessage = "File not found"; return; }
            using var fs = new FileStream(LogFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(fs);
            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                ParseLine(line);
            }
            _lastLen = fs.Length;
            RebuildCategoryLists();
            ApplyFilters();
            StatusMessage = $"Loaded {_all.Count} entries.";
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
        }
        finally { IsBusy = false; }
    }

    private void TailUpdate()
    {
        if (!Tail || string.IsNullOrWhiteSpace(LogFilePath) || !File.Exists(LogFilePath)) return;
        var info = new FileInfo(LogFilePath);
        if (info.Length == _lastLen) return;
        using var fs = new FileStream(LogFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        fs.Seek(_lastLen, SeekOrigin.Begin);
        using var reader = new StreamReader(fs);
        string? line;
        var added = false;
        while ((line = reader.ReadLine()) != null)
        {
            if (ParseLine(line)) added = true;
        }
        _lastLen = fs.Length;
        if (added)
        {
            RebuildCategoryLists();
            ApplyFilters(autoScroll: true);
        }
    }

    private bool ParseLine(string line)
    {
        var entry = LogParser.Parse(line);
        if (entry == null) return false;
        _all.Add(entry);
        TotalCount = _all.Count;
        return true;
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

    private void ApplyFilters(bool autoScroll = false)
    {
        var text = FilterText?.Trim();
        var list = _all.Where(e => LevelVisible(e.Level));
        if (!string.IsNullOrEmpty(SelectedCategory)) list = list.Where(e => e.Category == SelectedCategory);
        if (!string.IsNullOrEmpty(SelectedSubcategory)) list = list.Where(e => e.Subcategory == SelectedSubcategory);
        if (!string.IsNullOrEmpty(text)) list = list.Where(e => (e.Message?.IndexOf(text, StringComparison.OrdinalIgnoreCase) ?? -1) >= 0);
        var filtered = list.ToList();
        Entries.Clear();
        foreach (var e in filtered) Entries.Add(e);
        FilteredCount = filtered.Count;
        StatusMessage = $"Showing {FilteredCount}/{TotalCount}";
        // autoScroll hook would go here (emit event)
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _timer.Stop();
    }
}
