using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FileProcessor.Core;
using FileProcessor.Core.Workspace;
using System.Text.Json;
using System.Collections.Generic;
using System.Collections.Concurrent;

namespace FileProcessor.Infrastructure.Workspace;

// Lightweight static facade to manage a workspace DB lifetime for the app.
public static class WorkspaceDbService
{
    private static IWorkspaceDb? _db;
    private static long _sessionId;
    private static long _currentRunId;
    private static readonly ConcurrentQueue<LogWrite> _pending = new();

    public static IWorkspaceDb Db => _db ?? throw new InvalidOperationException("Workspace DB not initialized");
    public static long SessionId => _sessionId;
    public static long? SessionIdOrNull => _sessionId == 0 ? (long?)null : _sessionId;
    public static long CurrentRunId => _currentRunId;

    public static async Task InitializeAsync(CancellationToken ct = default)
    {
        var workspacePath = SettingsService.Instance.WorkspaceDirectory
            ?? throw new InvalidOperationException("Workspace directory is not configured");
        Directory.CreateDirectory(workspacePath);
        var dbPath = Path.Combine(workspacePath, "workspace.db");

        var db = new SqliteWorkspaceDb();
        await db.InitializeAsync(dbPath, ct);
        _sessionId = await db.StartSessionAsync(appVersion: typeof(WorkspaceDbService).Assembly.GetName().Version?.ToString(),
                                               userName: Environment.UserName,
                                               hostName: Environment.MachineName,
                                               ct: ct);
        _db = db;
        // flush any pending logs captured before init completed
        await FlushPendingAsync(ct);
    }

    // Accept a log write; buffer until DB/session is ready, otherwise append best-effort.
    public static void AppendOrBuffer(LogWrite write)
    {
        if (_db == null || _sessionId == 0)
        {
            _pending.Enqueue(write);
            return;
        }
        _ = SafeAppendAsync(write);
    }

    private static async Task SafeAppendAsync(LogWrite write)
    {
        try { await Db.AppendLogAsync(write); } catch { }
    }

    private static async Task FlushPendingAsync(CancellationToken ct)
    {
        if (_db == null) return;
        while (_pending.TryDequeue(out var w))
        {
            try { await Db.AppendLogAsync(w, ct); } catch { }
            if (ct.IsCancellationRequested) break;
        }
    }

    public static async Task<long> StartRunAsync(string type, string? name = null, string? metadataJson = null, long startedAtMs = 0, CancellationToken ct = default)
    {
        var id = await Db.StartRunAsync(SessionId, type, name, metadataJson, startedAtMs, ct);
        _currentRunId = id;
        return id;
    }

    public static async Task EndRunAsync(long? runId = null, string status = "succeeded", long endedAtMs = 0, CancellationToken ct = default)
    {
        var id = runId ?? _currentRunId;
        if (id != 0)
        {
            await Db.EndRunAsync(id, status, endedAtMs, ct);
            if (!runId.HasValue || runId == _currentRunId) _currentRunId = 0;
        }
    }

    public static async Task ShutdownAsync(CancellationToken ct = default)
    {
        if (_db != null && _sessionId != 0)
        {
            try { await _db.EndSessionAsync(_sessionId, ct); }
            catch { /* ignore on shutdown */ }
            finally { _db.Dispose(); _db = null; _sessionId = 0; _currentRunId = 0; }
        }
    }

    // Materialize logs for a specific run to a JSONL file. Returns the output path.
    public static async Task<string> MaterializeRunLogsAsync(long runId, string? outputPath = null, CancellationToken ct = default)
    {
        var workspacePath = SettingsService.Instance.WorkspaceDirectory
            ?? throw new InvalidOperationException("Workspace directory is not configured");
        var logsDir = Path.Combine(workspacePath, "logs");
        Directory.CreateDirectory(logsDir);
        var outPath = outputPath ?? Path.Combine(logsDir, $"run-{runId}.jsonl");

        await using var fs = new FileStream(outPath, FileMode.Create, FileAccess.Write, FileShare.Read);
        await using var sw = new StreamWriter(fs);
        var options = new JsonSerializerOptions { WriteIndented = false };

        int page = 0;
        while (true)
        {
            var rows = await Db.QueryLogsAsync(new LogQuery(RunId: runId, Page: page, PageSize: 2000), ct);
            if (rows.Count == 0) break;
            foreach (var row in rows)
            {
                JsonElement? data = null;
                if (!string.IsNullOrWhiteSpace(row.DataJson))
                {
                    try { data = JsonDocument.Parse(row.DataJson!).RootElement; } catch { }
                }
                var line = new MaterializedLog
                {
                    ts_ms = row.TsMs,
                    level = row.Level,
                    category = row.Category,
                    subcategory = row.Subcategory,
                    message = row.Message,
                    data = data,
                    run_id = row.RunId,
                    item_id = row.ItemId
                };
                var json = JsonSerializer.Serialize(line, options);
                await sw.WriteLineAsync(json);
            }
            page++;
            if (ct.IsCancellationRequested) break;
        }
        await sw.FlushAsync();
        return outPath;
    }

    // Materialize logs for the entire session to a JSONL file. Returns the output path.
    public static async Task<string> MaterializeSessionLogsAsync(long sessionId, string? outputPath = null, CancellationToken ct = default)
    {
        var workspacePath = SettingsService.Instance.WorkspaceDirectory
            ?? throw new InvalidOperationException("Workspace directory is not configured");
        var logsDir = Path.Combine(workspacePath, "logs");
        Directory.CreateDirectory(logsDir);
        var outPath = outputPath ?? Path.Combine(logsDir, $"session-{sessionId}.jsonl");

        await using var fs = new FileStream(outPath, FileMode.Create, FileAccess.Write, FileShare.Read);
        await using var sw = new StreamWriter(fs);
        var options = new JsonSerializerOptions { WriteIndented = false };

        int page = 0;
        while (true)
        {
            var rows = await Db.QueryLogsAsync(new LogQuery(SessionId: sessionId, Page: page, PageSize: 2000), ct);
            if (rows.Count == 0) break;
            foreach (var row in rows)
            {
                JsonElement? data = null;
                if (!string.IsNullOrWhiteSpace(row.DataJson))
                {
                    try { data = JsonDocument.Parse(row.DataJson!).RootElement; } catch { }
                }
                var line = new MaterializedLog
                {
                    ts_ms = row.TsMs,
                    level = row.Level,
                    category = row.Category,
                    subcategory = row.Subcategory,
                    message = row.Message,
                    data = data,
                    run_id = row.RunId,
                    item_id = row.ItemId
                };
                var json = JsonSerializer.Serialize(line, options);
                await sw.WriteLineAsync(json);
            }
            page++;
            if (ct.IsCancellationRequested) break;
        }
        await sw.FlushAsync();
        return outPath;
    }

    private sealed class MaterializedLog
    {
        public long ts_ms { get; set; }
        public int level { get; set; }
        public string? category { get; set; }
        public string? subcategory { get; set; }
        public string? message { get; set; }
        public JsonElement? data { get; set; }
        public long? run_id { get; set; }
        public long? item_id { get; set; }
    }
}
