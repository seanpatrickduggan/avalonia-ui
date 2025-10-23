using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FileProcessor.Core.Logging;
using FileProcessor.Core.Workspace;

namespace LogViewer.UI.ViewModels
{
    // Canonical LogViewerWindowViewModel moved into LogViewer.UI.
    // This uses FileProcessor.Core's LogParser when reading JSONL files.
    public partial class LogViewerWindowViewModel : ObservableObject, IDisposable
    {
        [ObservableProperty]
        private string _logFilePath = string.Empty;

        [ObservableProperty]
        private ObservableCollection<ItemLogEntry> _entries = new();

        [ObservableProperty]
        private ObservableCollection<LogGroupViewModel> _groups = new();

        [ObservableProperty]
        private string _filterText = string.Empty;

        [ObservableProperty]
        private bool _tail = true;

        [ObservableProperty]
        private bool _useDatabase = true;

        [ObservableProperty]
        private ObservableCollection<string> _categories = new();

        [ObservableProperty]
        private string? _selectedCategory;

        [ObservableProperty]
        private ObservableCollection<string> _subcategories = new();

        [ObservableProperty]
        private string? _selectedSubcategory;

        private readonly List<ItemLogEntry> _all = new();
        private readonly DispatcherTimer _timer;
        private readonly DispatcherTimer _filterTimer;

        // Optional runtime interfaces - LogViewer.UI can be used standalone without these.
        private readonly IWorkspaceRuntime? _runtime;
        private readonly IOperationContext? _opContext;
        private readonly FileProcessor.Core.Workspace.ILogReaderFactory? _readerFactory;
        private FileProcessor.Core.Workspace.ILogReader? _dbReader;
        private long _lastTsMsDb = 0;
        private bool _dbQueryInFlight = false;

        public IRelayCommand ExpandAllCommand { get; }
        public IRelayCommand CollapseAllCommand { get; }

        // Parameterless constructor for standalone use (file-only)
        public LogViewerWindowViewModel()
        {
            _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(700) };
            _timer.Tick += (_, _) => TailTick();
            _timer.Start();

            _filterTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
            _filterTimer.Tick += (_, _) => { _filterTimer.Stop(); ApplyFilters(); };

            ExpandAllCommand = new RelayCommand(() => { foreach (var g in Groups) g.IsExpanded = true; });
            CollapseAllCommand = new RelayCommand(() => { foreach (var g in Groups) g.IsExpanded = false; });
        }

        // Constructor for embedding with runtime support
        public LogViewerWindowViewModel(IWorkspaceRuntime runtime, IOperationContext opContext) : this()
        {
            _runtime = runtime;
            _opContext = opContext;
            LogFilePath = opContext.LogFilePath;
            // default to database mode when runtime is present
            UseDatabase = true;
            InitializeBackend();
        }

        // Constructor that accepts a reader factory for DB-backed tests and embedding
        public LogViewerWindowViewModel(IWorkspaceRuntime runtime, IOperationContext opContext, FileProcessor.Core.Workspace.ILogReaderFactory readerFactory) : this(runtime, opContext)
        {
            _readerFactory = readerFactory;
            UseDatabase = true;
            InitializeBackend();
        }

        private void TailTick()
        {
            if (!Tail) return;
            if (UseDatabase)
            {
                _ = QueryAndAppendDbAsync(initial: false);
                return;
            }
            if (!string.IsNullOrWhiteSpace(LogFilePath) && File.Exists(LogFilePath))
                LoadInitial();
        }

        public void LoadInitial()
        {
            _all.Clear();
            Entries.Clear();
            Groups.Clear();
            if (string.IsNullOrWhiteSpace(LogFilePath) || !File.Exists(LogFilePath)) return;
            try
            {
                foreach (var line in File.ReadLines(LogFilePath))
                {
                    var e = FileProcessor.Core.Logging.LogParser.Parse(line);
                    if (e != null) _all.Add(e);
                }
                RebuildCategoryLists();
                ApplyFilters();
            }
            catch { }
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

        private void ApplyFilters()
        {
            var text = FilterText?.Trim();
            var list = _all.AsEnumerable();
            if (!string.IsNullOrEmpty(SelectedCategory)) list = list.Where(e => e.Category == SelectedCategory);
            if (!string.IsNullOrEmpty(SelectedSubcategory)) list = list.Where(e => e.Subcategory == SelectedSubcategory);
            if (!string.IsNullOrEmpty(text)) list = list.Where(e => (e.Message?.IndexOf(text, StringComparison.OrdinalIgnoreCase) ?? -1) >= 0 || (e.Data is string ds && ds.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0));
            var filtered = list.ToList();

            Entries.Clear();
            foreach (var e in filtered) Entries.Add(e);

            Groups.Clear();
            var grouped = filtered
                .GroupBy(e => new { e.Category, e.Subcategory })
                .OrderBy(g => g.Key.Category)
                .ThenBy(g => g.Key.Subcategory)
                .Select(g => new LogGroupViewModel(g.Key.Category, g.Key.Subcategory, g.Count(), g.Max(e => e.Level), new ObservableCollection<ItemLogEntry>(g.OrderBy(e => e.TsUtc))));
            foreach (var g in grouped) Groups.Add(g);
        }

        partial void OnFilterTextChanged(string value) => ApplyFilters();
        partial void OnSelectedCategoryChanged(string? value) { UpdateSubcategoryList(); ApplyFilters(); }
        partial void OnSelectedSubcategoryChanged(string? value) => ApplyFilters();
        partial void OnUseDatabaseChanged(bool value)
        {
            InitializeBackend();
        }

        private void InitializeBackend()
        {
            _all.Clear();
            Entries.Clear();
            Groups.Clear();
            _lastTsMsDb = 0;
            _dbReader = null;
            if (UseDatabase && _readerFactory != null && _runtime != null)
            {
                _dbReader = _readerFactory.ForDatabase();
                _ = QueryAndAppendDbAsync(initial: true);
            }
            else
            {
                // fallback to file mode
                LoadInitial();
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
                IReadOnlyList<FileProcessor.Core.Workspace.LogRow> rows;
                try
                {
                    rows = await _dbReader.QueryLogsAsync(q);
                }
                catch (Exception ex)
                {
                    // Log DB query exceptions at debug level so standalone runs remain resilient but errors are visible
                    try { Serilog.Log.Debug(ex, "LogViewer: DB query failed"); } catch { }
                    return;
                }
                if (rows.Count == 0 && !initial) return;
                bool added = false;
                foreach (var r in rows)
                {
                    var ts = DateTimeOffset.FromUnixTimeMilliseconds(r.TsMs).UtcDateTime;
                    var sev = (FileProcessor.Core.Logging.LogSeverity)Math.Clamp(r.Level, 0, 5);
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

        private FileProcessor.Core.Workspace.LogQuery BuildQuery(long fromTsMs = 0)
        {
            int? min = null;
            // basic mapping: no level filtering for now
            long? opId = _runtime?.CurrentOperationId == 0 ? (long?)null : _runtime?.CurrentOperationId;
            long? sessionId = _runtime?.SessionId == 0 ? (long?)null : _runtime?.SessionId;
            return new FileProcessor.Core.Workspace.LogQuery(
                OperationId: opId,
                ItemId: null,
                MinLevel: min,
                MaxLevel: null,
                Category: SelectedCategory,
                Subcategory: SelectedSubcategory,
                TextContains: string.IsNullOrWhiteSpace(FilterText) ? null : FilterText,
                Page: 0,
                PageSize: 2000,
                FromTsMs: fromTsMs > 0 ? fromTsMs : null,
                ToTsMs: null,
                SessionId: opId == null ? sessionId : null);
        }

        public void Dispose()
        {
            try { _timer.Stop(); } catch { }
            try { _filterTimer.Stop(); } catch { }
        }
    }
}
