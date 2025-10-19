using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input; // added
using FileProcessor.Infrastructure.Logging;
using System.Collections.ObjectModel;
using FileProcessor.Core.Logging;
using System.IO;
using System.Text.Json;
using System;
using System.Linq;
using Avalonia.Threading;
using System.Collections.Generic;
using FileProcessor.Core.Workspace; // added
using System.Threading.Tasks; // added for Task

namespace FileProcessor.UI.ViewModels;

public partial class LogViewerWindowViewModel : ObservableObject, IDisposable
{
    [ObservableProperty]
    private string _logFilePath = string.Empty;

    [ObservableProperty]
    private ObservableCollection<ItemLogEntry> _entries = new(); // flat (legacy)

    // New grouped view
    [ObservableProperty]
    private ObservableCollection<LogGroupViewModel> _groups = new();

    [ObservableProperty]
    private string _filterText = string.Empty;

    [ObservableProperty]
    private bool _tail = true; // auto follow

    [ObservableProperty]
    private bool _showTrace = true;
    [ObservableProperty]
    private bool _showDebug = true;
    [ObservableProperty]
    private bool _showInfo = true;
    [ObservableProperty]
    private bool _showWarning = true;
    [ObservableProperty]
    private bool _showError = true;
    [ObservableProperty]
    private bool _showCritical = true;

    [ObservableProperty]
    private ObservableCollection<string> _categories = new();
    [ObservableProperty]
    private string? _selectedCategory;

    [ObservableProperty]
    private ObservableCollection<string> _subcategories = new();
    [ObservableProperty]
    private string? _selectedSubcategory;

    [ObservableProperty]
    private string _combinedText = string.Empty; // aggregated view

    // Backend selection
    [ObservableProperty]
    private bool _useDatabase = true; // toggle between DB and JSONL

    private long _lastLength;
    private readonly DispatcherTimer _timer;
    private readonly List<ItemLogEntry> _all = new();

    // Debounce timer for filters/search
    private readonly DispatcherTimer _filterTimer;

    // DB reader state
    private ILogReader? _dbReader;
    private long _lastTsMsDb = 0;
    private bool _dbQueryInFlight = false;
    private bool _tailInFlight = false;

    // Injected workspace runtime
    private readonly IWorkspaceRuntime _runtime;
    private readonly IOperationContext _opContext;
    private readonly ILogReaderFactory _readerFactory;

    public IRelayCommand ExpandAllCommand { get; }
    public IRelayCommand CollapseAllCommand { get; }

    public LogViewerWindowViewModel(IWorkspaceRuntime runtime, IOperationContext opContext, ILogReaderFactory readerFactory)
    {
        _runtime = runtime;
        _opContext = opContext;
        _readerFactory = readerFactory;
        LogFilePath = _opContext.LogFilePath;
        // default to DB if available (current operation/session exists)
        UseDatabase = true;
        InitializeBackend();
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(700) };
        _timer.Tick += async (_, _) => await OnTickAsync();
        _timer.Start();

        _filterTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        _filterTimer.Tick += (_, _) => { _filterTimer.Stop(); ApplyFilters(); };

        ExpandAllCommand = new RelayCommand(() => { foreach (var g in Groups) g.IsExpanded = true; });
        CollapseAllCommand = new RelayCommand(() => { foreach (var g in Groups) g.IsExpanded = false; });
    }

    partial void OnFilterTextChanged(string value) => DebounceFilters();
    partial void OnSelectedCategoryChanged(string? value) { UpdateSubcategoryList(); DebounceFilters(); }
    partial void OnSelectedSubcategoryChanged(string? value) => DebounceFilters();
    partial void OnShowTraceChanged(bool value) => DebounceFilters();
    partial void OnShowDebugChanged(bool value) => DebounceFilters();
    partial void OnShowInfoChanged(bool value) => DebounceFilters();
    partial void OnShowWarningChanged(bool value) => DebounceFilters();
    partial void OnShowErrorChanged(bool value) => DebounceFilters();
    partial void OnShowCriticalChanged(bool value) => DebounceFilters();
    partial void OnUseDatabaseChanged(bool value)
    {
        // reset any in-flight work when switching backends
        _dbReader = null;
        _dbQueryInFlight = false;
        _tailInFlight = false;
        InitializeBackend();
    }

    private void DebounceFilters()
    {
        _filterTimer.Stop();
        _filterTimer.Start();
    }

    private void InitializeBackend()
    {
        _all.Clear();
        Entries.Clear();
        Groups.Clear();
        _lastLength = 0;
        _lastTsMsDb = 0;
        if (UseDatabase)
        {
            _dbReader = _readerFactory.ForDatabase();
            // initial load from DB
            _ = QueryAndAppendDbAsync(initial: true);
        }
        else
        {
            _dbReader = null;
            // fallback to JSONL file tailing (Serilog compact JSON)
            LoadInitial();
        }
    }

    private LogQuery BuildQuery(long fromTsMs = 0)
    {
        int? min = null, max = null;
        if (!ShowTrace) min = (int)LogSeverity.Debug;
        if (!ShowDebug && (min ?? 0) <= (int)LogSeverity.Debug) min = (int)LogSeverity.Info;
        if (!ShowInfo && (min ?? 0) <= (int)LogSeverity.Info) min = (int)LogSeverity.Warning;
        if (!ShowWarning && (min ?? 0) <= (int)LogSeverity.Warning) min = (int)LogSeverity.Error;
        if (!ShowError && (min ?? 0) <= (int)LogSeverity.Error) min = (int)LogSeverity.Critical;
        // max remains null unless user adds UI for it

        long? opId = _runtime.CurrentOperationId == 0 ? (long?)null : _runtime.CurrentOperationId;
        long? sessionId = _runtime.SessionId == 0 ? (long?)null : _runtime.SessionId;

        return new LogQuery(
            OperationId: opId,
            ItemId: null,
            MinLevel: min,
            MaxLevel: max,
            Category: SelectedCategory,
            Subcategory: SelectedSubcategory,
            TextContains: string.IsNullOrWhiteSpace(FilterText) ? null : FilterText,
            Page: 0,
            PageSize: 2000,
            FromTsMs: fromTsMs > 0 ? fromTsMs : null,
            ToTsMs: null,
            SessionId: opId == null ? sessionId : null);
    }

    private async Task OnTickAsync()
    {
        if (UseDatabase)
        {
            if (!Tail) return;
            await QueryAndAppendDbAsync(initial: false);
        }
        else
        {
            TailUpdate();
        }
    }

    private async Task QueryAndAppendDbAsync(bool initial)
    {
        if (_dbReader == null) return;
        if (_dbQueryInFlight) return;
        _dbQueryInFlight = true;
        try
        {
            var q = BuildQuery(fromTsMs: _lastTsMsDb + 1);
            IReadOnlyList<LogRow> rows;
            try { rows = await _dbReader.QueryLogsAsync(q); } catch { return; }
            if (rows.Count == 0 && !initial) return;
            bool added = false;
            foreach (var r in rows)
            {
                var ts = DateTimeOffset.FromUnixTimeMilliseconds(r.TsMs).UtcDateTime;
                var sev = (LogSeverity)Math.Clamp(r.Level, 0, 5);
                var entry = new ItemLogEntry(ts, sev, r.Category ?? string.Empty, r.Subcategory ?? string.Empty, r.Message ?? string.Empty, r.DataJson);
                _all.Add(entry);
                if (r.TsMs > _lastTsMsDb) _lastTsMsDb = r.TsMs;
                added = true;
            }
            if (added)
            {
                RebuildCategoryLists();
                ApplyFilters();
            }
        }
        finally
        {
            _dbQueryInFlight = false;
        }
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
        if (_tailInFlight) return;
        _tailInFlight = true;
        try
        {
            // Detect log file path changes (new operation)
            if (!string.Equals(_opContext.LogFilePath, LogFilePath, StringComparison.Ordinal))
            {
                LogFilePath = _opContext.LogFilePath;
                LoadInitial();
                return;
            }

            if (!Tail) return;
            if (string.IsNullOrEmpty(LogFilePath) || !File.Exists(LogFilePath)) return;
            var info = new FileInfo(LogFilePath);
            if (info.Length == _lastLength) return; // no growth
            using var fs = new FileStream(LogFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            fs.Seek(_lastLength, SeekOrigin.Begin);
            using var reader = new StreamReader(fs);
            string? line;
            var added = false;
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
        finally
        {
            _tailInFlight = false;
        }
    }

    private bool ParseAndAdd(string line)
    {
        try
        {
            using var doc = JsonDocument.Parse(line);
            var root = doc.RootElement;

            // Timestamp: handle Serilog '@t' as well as 'Timestamp'
            DateTime ts = DateTime.UtcNow;
            if (root.TryGetProperty("Timestamp", out var tEl) || root.TryGetProperty("@t", out tEl))
            {
                if (tEl.ValueKind == JsonValueKind.String)
                {
                    var s = tEl.GetString();
                    if (!string.IsNullOrEmpty(s) && DateTime.TryParse(s, out var parsed)) ts = parsed;
                }
                else if (tEl.ValueKind == JsonValueKind.Number && tEl.TryGetDateTime(out var parsedNum))
                {
                    ts = parsedNum;
                }
            }

            // Message: prefer RenderedMessage or Message, fall back to templates
            var msg = string.Empty;
            if (root.TryGetProperty("RenderedMessage", out var rmEl)) msg = rmEl.GetString() ?? string.Empty;
            else if (root.TryGetProperty("Message", out var mEl)) msg = mEl.GetString() ?? string.Empty;
            else if (root.TryGetProperty("@mt", out var mtEl)) msg = mtEl.GetString() ?? string.Empty;
            else if (root.TryGetProperty("MessageTemplate", out var mt2El)) msg = mt2El.GetString() ?? string.Empty;

            // Level: handle '@l' (Serilog) or 'Level'
            var levelStr = "Information";
            if (root.TryGetProperty("Level", out var lvlEl) && lvlEl.ValueKind == JsonValueKind.String) levelStr = lvlEl.GetString() ?? levelStr;
            else if (root.TryGetProperty("@l", out var lvl2El) && lvl2El.ValueKind == JsonValueKind.String) levelStr = lvl2El.GetString() ?? levelStr;

            LogSeverity severity = levelStr switch { "Verbose" => LogSeverity.Trace, "Debug" => LogSeverity.Debug, "Information" => LogSeverity.Info, "Warning" => LogSeverity.Warning, "Error" => LogSeverity.Error, "Fatal" => LogSeverity.Critical, _ => LogSeverity.Info };

            string cat = string.Empty; string sub = string.Empty; string? dataStr = null;

            // Top-level cat/sub/severityRank/data or inside Properties
            if (root.TryGetProperty("cat", out var catEl)) cat = catEl.ToString();
            if (root.TryGetProperty("sub", out var subEl)) sub = subEl.ToString();

            if (root.TryGetProperty("severityRank", out var rankEl))
            {
                if (rankEl.ValueKind == JsonValueKind.Number)
                {
                    try
                    {
                        var rank = rankEl.GetInt32();
                        if (rank >= 0 && rank <= 5) severity = (LogSeverity)rank;
                    }
                    catch { }
                }
            }

            if (root.TryGetProperty("Data", out var dataEl))
            {
                // keep raw JSON snippet
                dataStr = dataEl.GetRawText();
            }

            if (root.TryGetProperty("Properties", out var props))
            {
                if (string.IsNullOrEmpty(cat) && props.TryGetProperty("cat", out var cat2)) cat = cat2.ToString();
                if (string.IsNullOrEmpty(sub) && props.TryGetProperty("sub", out var sub2)) sub = sub2.ToString();
                if (props.TryGetProperty("severityRank", out var rank2))
                {
                    try
                    {
                        var rank = rank2.GetInt32();
                        if (rank >= 0 && rank <= 5) severity = (LogSeverity)rank;
                    }
                    catch { }
                }
                if (dataStr == null && props.TryGetProperty("data", out var data2)) dataStr = data2.GetRawText();
            }

            var entry = new ItemLogEntry(ts, severity, cat, sub, msg, dataStr);
            _all.Add(entry);
            return true;
        }
        catch
        {
            return false;
        }
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
        if (!string.IsNullOrEmpty(text)) list = list.Where(e => (e.Message?.IndexOf(text, StringComparison.OrdinalIgnoreCase) ?? -1) >= 0 || (e.Data is string ds && ds.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0));
        var filtered = list.ToList();

        // Rebuild flat list (optional)
        Entries.Clear();
        foreach (var e in filtered) Entries.Add(e);

        // Rebuild grouped view
        var grouped = filtered
            .GroupBy(e => new { e.Category, e.Subcategory })
            .OrderBy(g => g.Key.Category)
            .ThenBy(g => g.Key.Subcategory)
            .Select(g => new LogGroupViewModel(
                g.Key.Category,
                g.Key.Subcategory,
                g.Count(),
                g.Max(e => e.Level),
                new ObservableCollection<ItemLogEntry>(g.OrderBy(e => e.TsUtc))));
        Groups.Clear();
        foreach (var g in grouped)
            Groups.Add(g);

        // Combined text (still available if needed elsewhere)
        var sb = new System.Text.StringBuilder(filtered.Count * 80);
        foreach (var e in filtered)
        {
            sb.Append(e.TsUtc.ToString("HH:mm:ss.fff"))
              .Append(" | ")
              .Append(e.Level.ToString().PadRight(5))
              .Append(" | ")
              .Append(string.IsNullOrEmpty(e.Category) ? "-" : e.Category)
              .Append('/')
              .Append(string.IsNullOrEmpty(e.Subcategory) ? "-" : e.Subcategory)
              .Append(": ")
              .Append(e.Message ?? string.Empty);
            if (e.Data is string ds)
            {
                sb.Append(" | data=").Append(ds.Replace('\n',' ').Replace("  "," "));
            }
            sb.Append('\n');
        }
        CombinedText = sb.ToString();
    }

    public void Dispose()
    {
        try { _timer.Stop(); } catch { }
        try { _filterTimer.Stop(); } catch { }
    }
}

public partial class LogGroupViewModel : ObservableObject
{
    public string Category { get; }
    public string Subcategory { get; }
    public int Count { get; }
    public LogSeverity HighestSeverity { get; }
    public ObservableCollection<ItemLogEntry> Entries { get; }

    [ObservableProperty]
    private bool _isExpanded; // user toggle

    public RelayCommand ToggleCommand { get; }

    public string Header => $"{Subcategory} ({Count})";

    public string SeveritySummary { get; }

    public LogGroupViewModel(string category, string subcategory, int count, LogSeverity highest, ObservableCollection<ItemLogEntry> entries)
    {
        Category = category;
        Subcategory = subcategory;
        Count = count;
        HighestSeverity = highest;
        Entries = entries;
        // Auto expand if severity >= Error
        _isExpanded = highest >= LogSeverity.Error;
        ToggleCommand = new RelayCommand(() => IsExpanded = !IsExpanded);

        int trace = entries.Count(e => e.Level == LogSeverity.Trace);
        int debug = entries.Count(e => e.Level == LogSeverity.Debug);
        int info = entries.Count(e => e.Level == LogSeverity.Info);
        int warn = entries.Count(e => e.Level == LogSeverity.Warning);
        int error = entries.Count(e => e.Level == LogSeverity.Error);
        int crit = entries.Count(e => e.Level == LogSeverity.Critical);
        var parts = new System.Collections.Generic.List<string>();
        if (trace > 0) parts.Add($"trace {trace}");
        if (debug > 0) parts.Add($"debug {debug}");
        if (info > 0) parts.Add($"info {info}");
        if (warn > 0) parts.Add($"warn {warn}");
        if (error > 0) parts.Add($"error {error}");
        if (crit > 0) parts.Add($"crit {crit}");
        SeveritySummary = string.Join(", ", parts);
    }
}
