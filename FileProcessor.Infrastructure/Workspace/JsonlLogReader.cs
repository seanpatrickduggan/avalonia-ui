using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FileProcessor.Core.Workspace;

namespace FileProcessor.Infrastructure.Workspace;

public sealed class JsonlLogReader : ILogReader
{
    private readonly string _filePath;
    public JsonlLogReader(string filePath) => _filePath = filePath;

    public Task<IReadOnlyList<LogRow>> QueryLogsAsync(LogQuery query, CancellationToken ct = default)
    {
        // naive streaming implementation; filter client-side for now
        var list = new List<LogRow>(query.PageSize);
        if (!File.Exists(_filePath)) return Task.FromResult<IReadOnlyList<LogRow>>(list);
        using var fs = new FileStream(_filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var sr = new StreamReader(fs);
        long rowId = 0;
        long offset = query.Page * query.PageSize;
        long taken = 0;
        string? line;
        while ((line = sr.ReadLine()) != null)
        {
            if (ct.IsCancellationRequested) break;
            try
            {
                using var doc = JsonDocument.Parse(line);
                var root = doc.RootElement;
                long ts = 0;
                if (root.TryGetProperty("ts_ms", out var tsEl) && tsEl.TryGetInt64(out var tsv)) ts = tsv;
                int lvl = 2;
                if (root.TryGetProperty("level", out var lvEl) && lvEl.TryGetInt32(out var lvv)) lvl = lvv;
                string? cat = root.TryGetProperty("category", out var cEl) && cEl.ValueKind == JsonValueKind.String ? cEl.GetString() : null;
                string? sub = root.TryGetProperty("subcategory", out var sEl) && sEl.ValueKind == JsonValueKind.String ? sEl.GetString() : null;
                string? msg = root.TryGetProperty("message", out var mEl) && mEl.ValueKind == JsonValueKind.String ? mEl.GetString() : null;
                string? data = root.TryGetProperty("data", out var dEl) ? dEl.GetRawText() : null;
                long? opId = root.TryGetProperty("operation_id", out var oEl) && oEl.TryGetInt64(out var ov) ? ov : null;
                long? itemId = root.TryGetProperty("item_id", out var iEl) && iEl.TryGetInt64(out var iv) ? iv : null;

                // basic filtering
                if (query.OperationId is long qo && opId != qo) continue;
                if (query.ItemId is long qi && itemId != qi) continue;
                if (query.MinLevel is int qmin && lvl < qmin) continue;
                if (query.MaxLevel is int qmax && lvl > qmax) continue;
                if (!string.IsNullOrWhiteSpace(query.Category) && !string.Equals(cat, query.Category)) continue;
                if (!string.IsNullOrWhiteSpace(query.Subcategory) && !string.Equals(sub, query.Subcategory)) continue;
                if (query.FromTsMs is long qf && ts < qf) continue;
                if (query.ToTsMs is long qt && ts > qt) continue;
                if (!string.IsNullOrWhiteSpace(query.TextContains) && (msg == null || msg.IndexOf(query.TextContains, System.StringComparison.OrdinalIgnoreCase) < 0)) continue;

                // paging
                if (offset > 0) { offset--; continue; }
                if (taken >= query.PageSize) break;

                list.Add(new LogRow(++rowId, ts, lvl, cat, sub, msg, data, opId, itemId));
                taken++;
            }
            catch { /* ignore malformed lines */ }
        }
        return Task.FromResult<IReadOnlyList<LogRow>>(list);
    }

    public Task<IReadOnlyList<LogGroupCount>> QueryGroupCountsAsync(LogQuery query, CancellationToken ct = default)
    {
        // naive pass to compute grouped counts (for small–medium files it's fine)
        var groups = new Dictionary<(string? cat, string? sub), (int count, int maxLvl)>();
        var rowsTask = QueryLogsAsync(query with { Page = 0, PageSize = int.MaxValue }, ct);
        var rows = rowsTask.GetAwaiter().GetResult();
        foreach (var r in rows)
        {
            var key = (r.Category, r.Subcategory);
            if (!groups.TryGetValue(key, out var val)) val = (0, -1);
            val.count++;
            if (r.Level > val.maxLvl) val.maxLvl = r.Level;
            groups[key] = val;
        }
        var list = new List<LogGroupCount>(groups.Count);
        foreach (var kv in groups)
            list.Add(new LogGroupCount(kv.Key.cat, kv.Key.sub, kv.Value.count, kv.Value.maxLvl));
        return Task.FromResult<IReadOnlyList<LogGroupCount>>(list);
    }
}
