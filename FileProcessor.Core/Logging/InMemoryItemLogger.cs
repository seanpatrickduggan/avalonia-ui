using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Threading;

namespace FileProcessor.Core.Logging;

internal sealed class InMemoryItemLogScope : IItemLogScope
{
    private readonly List<ItemLogEntry> _entries;
    private readonly ItemLogOptions _options;
    private readonly IRunStructuredLogger? _runLogger;
    private readonly Guid _runId;
    private readonly string? _batchType;
    private bool _disposed;
    private bool _spilled;
    private string? _spillFile;
    private LogSeverity _highest = LogSeverity.Info;
    private readonly Dictionary<LogSeverity, int> _levelCounts = new();

    public InMemoryItemLogScope(string itemId, ItemLogOptions options, Guid runId, string? batchType, IRunStructuredLogger? runLogger)
    {
        ItemId = itemId;
        _options = options;
        _runId = runId;
        _batchType = batchType;
        _runLogger = runLogger;
        _entries = new List<ItemLogEntry>(options.InitialCapacity);
    }

    public string ItemId { get; }

    public ItemLogResult Result => BuildResult();

    public void Log(LogSeverity level, string category, string subcategory, string message, object? data = null)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(InMemoryItemLogScope));
        if (_spilled && _options.Overflow == ItemLogOverflowPolicy.SpillToDisk)
        {
            // Still forward to run logger even if local spill done.
            _runLogger?.Log(_runId, _batchType, ItemId, level, category, subcategory, message, data);
            UpdateHighest(level);
            IncrementCount(level); // counts remain even if not in memory list
            AppendToSpill(level, category, subcategory, message, data);
            return;
        }

        if (_entries.Count >= _options.MaxEntries)
        {
            switch (_options.Overflow)
            {
                case ItemLogOverflowPolicy.StopRecording:
                    return; // ignore further entries locally, still could forward global
                case ItemLogOverflowPolicy.TruncateAndFlag:
                    return; // simply stop; truncated flag will be true
                case ItemLogOverflowPolicy.SpillToDisk:
                    SpillToDisk();
                    break;
            }
        }

        var entry = new ItemLogEntry(DateTime.UtcNow, level, category, subcategory, message, data);
        if (!_spilled) _entries.Add(entry);
        else AppendToSpill(entry.Level, entry.Category, entry.Subcategory, entry.Message, entry.Data);

        UpdateHighest(level);
        IncrementCount(level);

        _runLogger?.Log(_runId, _batchType, ItemId, level, category, subcategory, message, data);
    }

    private void UpdateHighest(LogSeverity level)
    {
        if (level > _highest) _highest = level;
    }

    private void IncrementCount(LogSeverity level)
    {
        if (_levelCounts.TryGetValue(level, out var c)) _levelCounts[level] = c + 1; else _levelCounts[level] = 1;
    }

    private void SpillToDisk()
    {
        if (_spilled) return;
        Directory.CreateDirectory(_options.SpillDirectory);
        _spillFile = Path.Combine(_options.SpillDirectory, $"{SanitizeFileName(ItemId)}_{Guid.NewGuid():N}.jsonl");
        using (var fs = new FileStream(_spillFile, FileMode.CreateNew, FileAccess.Write, FileShare.Read))
        using (var writer = new StreamWriter(fs))
        {
            foreach (var e in _entries)
            {
                WriteJsonLine(writer, e);
            }
        }
        _entries.Clear();
        _spilled = true;
    }

    private void AppendToSpill(LogSeverity level, string category, string subcategory, string message, object? data)
    {
        if (_spillFile == null) return; // should not happen
        using var fs = new FileStream(_spillFile, FileMode.Append, FileAccess.Write, FileShare.Read);
        using var writer = new StreamWriter(fs);
        WriteJsonLine(writer, new ItemLogEntry(DateTime.UtcNow, level, category, subcategory, message, data));
    }

    private static void WriteJsonLine(StreamWriter writer, ItemLogEntry e)
    {
        var json = JsonSerializer.Serialize(new
        {
            ts = e.TsUtc,
            level = e.Level.ToString(),
            cat = e.Category,
            sub = e.Subcategory,
            msg = e.Message,
            data = e.Data
        });
        writer.WriteLine(json);
    }

    private static string SanitizeFileName(string s)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
        {
            s = s.Replace(c, '_');
        }
        return s.Length == 0 ? "item" : s;
    }

    private ItemLogResult BuildResult()
    {
        var roEntries = new ReadOnlyCollection<ItemLogEntry>(_entries);
        var levelCounts = new ReadOnlyDictionary<LogSeverity, int>(new Dictionary<LogSeverity, int>(_levelCounts));
        var truncated = _entries.Count >= _options.MaxEntries && _options.Overflow == ItemLogOverflowPolicy.TruncateAndFlag;
        return new ItemLogResult(ItemId, _highest, _entries.Count, roEntries, levelCounts, truncated, _spillFile);
    }

    public void Dispose()
    {
        _disposed = true;
    }
}

public sealed class ItemLogFactory : IItemLogFactory
{
    private readonly ItemLogOptions _options;
    private readonly IRunStructuredLogger? _runLogger;
    private readonly Func<Guid> _runIdProvider;
    private readonly Func<string?> _batchTypeProvider;

    public ItemLogFactory(ItemLogOptions options, IRunStructuredLogger? runLogger, Func<Guid>? runIdProvider = null, Func<string?>? batchTypeProvider = null)
    {
        _options = options;
        _runLogger = runLogger;
        _runIdProvider = runIdProvider ?? (() => Guid.Empty);
        _batchTypeProvider = batchTypeProvider ?? (() => null);
    }

    public IItemLogScope Start(string itemId)
    {
        return new InMemoryItemLogScope(itemId, _options, _runIdProvider(), _batchTypeProvider(), _runLogger);
    }
}
