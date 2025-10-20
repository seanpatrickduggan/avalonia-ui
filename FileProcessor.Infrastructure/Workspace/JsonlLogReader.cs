using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FileProcessor.Core.Workspace;
using FileProcessor.Core.Abstractions;
using FileProcessor.Infrastructure.Workspace.Jsonl;

namespace FileProcessor.Infrastructure.Workspace;

public sealed class JsonlLogReader : ILogReader
{
    private readonly string _filePath;
    private readonly IFileSystem _fs;
    public JsonlLogReader(string filePath) : this(filePath, new FileProcessor.Infrastructure.Abstractions.SystemFileSystem()) {}
    public JsonlLogReader(string filePath, IFileSystem fs) { _filePath = filePath; _fs = fs; }

    public async Task<IReadOnlyList<LogRow>> QueryLogsAsync(LogQuery query, CancellationToken ct = default)
    {
        var list = new List<LogRow>(Math.Min(query.PageSize, 512));
        if (!_fs.FileExists(_filePath)) return list;
        using var fs = _fs.CreateFile(_filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var sr = new StreamReader(fs);

        long rowId = 0;
        long skip = (long)query.Page * query.PageSize;
        int taken = 0;
        var predicate = JsonlFilters.BuildPredicate(query);

        string? line;
        while (!ct.IsCancellationRequested && (line = await sr.ReadLineAsync(ct)) != null)
        {
            if (!JsonlLineParser.TryParseLine(line, out var p)) continue;
            if (!predicate(p)) continue;
            if (skip > 0) { skip--; continue; }
            if (taken >= query.PageSize) break;
            list.Add(ToLogRow(++rowId, p));
            taken++;
        }
        return list;
    }

    public async Task<IReadOnlyList<LogGroupCount>> QueryGroupCountsAsync(LogQuery query, CancellationToken ct = default)
    {
        var result = new List<LogGroupCount>();
        if (!_fs.FileExists(_filePath)) return result;
        var predicate = JsonlFilters.BuildPredicate(query);
        var groups = new Dictionary<(string? cat, string? sub), (int count, int maxLvl)>();
        using var fs = _fs.CreateFile(_filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var sr = new StreamReader(fs);

        string? line;
        while (!ct.IsCancellationRequested && (line = await sr.ReadLineAsync(ct)) != null)
        {
            if (!JsonlLineParser.TryParseLine(line, out var p)) continue;
            if (!predicate(p)) continue;
            var key = (p.Category, p.Subcategory);
            if (!groups.TryGetValue(key, out var val)) val = (0, -1);
            val.count++;
            if (p.Level > val.maxLvl) val.maxLvl = p.Level;
            groups[key] = val;
        }
        foreach (var kv in groups)
            result.Add(new LogGroupCount(kv.Key.cat, kv.Key.sub, kv.Value.count, kv.Value.maxLvl));
        return result;
    }

    private static LogRow ToLogRow(long id, in JsonlLineParser.ParsedLog p)
        => new(id, p.Ts, p.Level, p.Category, p.Subcategory, p.Message, p.DataJson, p.OperationId, p.ItemId);
}
