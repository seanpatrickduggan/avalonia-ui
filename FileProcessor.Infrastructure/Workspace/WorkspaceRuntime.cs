using System.Text.Json;
using System.Collections.Concurrent;
using System.Threading.Channels;
using FileProcessor.Core.Interfaces;
using FileProcessor.Core.Workspace;
using FileProcessor.Core.Abstractions; // IFileSystem only

namespace FileProcessor.Infrastructure.Workspace;

// Instance-based runtime owning the workspace DB and the bounded writer
public sealed class WorkspaceRuntime : IWorkspaceRuntime, ILogAppender, IDisposable
{
    private readonly IWorkspaceDb _db;
    private readonly ISettingsService _settings;
    private readonly IFileSystem _fs;

    private long _sessionId;
    private long _currentOperationId;

    private readonly ConcurrentQueue<LogWrite> _pending = new();
    private readonly object _initLock = new();
    private Task? _initTask;

    private ChannelWriterImpl? _writer;

    public WorkspaceRuntime(IWorkspaceDb db, ISettingsService settings, IFileSystem fs)
    {
        _db = db;
        _settings = settings;
        _fs = fs;
    }

    public long SessionId => _sessionId;
    public long CurrentOperationId => _currentOperationId;

    public Task InitializeAsync(CancellationToken ct = default)
    {
        lock (_initLock)
        {
            if (_initTask != null) return _initTask;
            _initTask = InitializeCoreAsync(ct);
            return _initTask;
        }
    }

    private async Task InitializeCoreAsync(CancellationToken ct)
    {
        try
        {
            var workspacePath = _settings.WorkspaceDirectory
                ?? throw new InvalidOperationException("Workspace directory is not configured");
            _fs.CreateDirectory(workspacePath);
            var dbPath = Path.Combine(workspacePath, "workspace.db");

            await _db.InitializeAsync(dbPath, ct);
            _sessionId = await _db.StartSessionAsync(appVersion: typeof(WorkspaceRuntime).Assembly.GetName().Version?.ToString(),
                                                    userName: Environment.UserName,
                                                    hostName: Environment.MachineName,
                                                    ct: ct);
            // flush pending captured before init
            await FlushPendingAsync(ct);
            _writer ??= new ChannelWriterImpl(_db);
        }
        finally
        {
            lock (_initLock) { _initTask = null; }
        }
    }

    public async Task<long> StartOperationAsync(string type, string? name = null, string? metadataJson = null, long startedAtMs = 0, CancellationToken ct = default)
    {
        var id = await _db.StartOperationAsync(_sessionId, type, name, metadataJson, startedAtMs, ct);
        _currentOperationId = id;
        return id;
    }

    public async Task EndOperationAsync(string status = "succeeded", long endedAtMs = 0, CancellationToken ct = default)
    {
        var id = _currentOperationId;
        if (id != 0)
        {
            await _db.EndOperationAsync(id, status, endedAtMs, ct);
            _currentOperationId = 0;
        }
    }

    public async Task<string> MaterializeOperationLogsAsync(long operationId, string? outputPath = null, CancellationToken ct = default)
    {
        var workspacePath = _settings.WorkspaceDirectory
            ?? throw new InvalidOperationException("Workspace directory is not configured");
        var logsDir = Path.Combine(workspacePath, "logs");
        _fs.CreateDirectory(logsDir);
        var outPath = outputPath ?? Path.Combine(logsDir, $"operation-{operationId}.jsonl");

        await using var fs = _fs.CreateFile(outPath, FileMode.Create, FileAccess.Write, FileShare.Read);
        await using var sw = new StreamWriter(fs);
        var options = new System.Text.Json.JsonSerializerOptions { WriteIndented = false };

        int page = 0;
        while (true)
        {
            var rows = await _db.QueryLogsAsync(new LogQuery(OperationId: operationId, Page: page, PageSize: 2000), ct);
            if (rows.Count == 0) break;
            foreach (var row in rows)
            {
                System.Text.Json.JsonElement? data = null;
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
                    operation_id = row.OperationId,
                    item_id = row.ItemId
                };
                var json = System.Text.Json.JsonSerializer.Serialize(line, options);
                await sw.WriteLineAsync(json);
            }
            page++;
            if (ct.IsCancellationRequested) break;
        }
        await sw.FlushAsync();
        return outPath;
    }

    public async Task<string> MaterializeSessionLogsAsync(long sessionId, string? outputPath = null, CancellationToken ct = default)
    {
        var workspacePath = _settings.WorkspaceDirectory
            ?? throw new InvalidOperationException("Workspace directory is not configured");
        var logsDir = Path.Combine(workspacePath, "logs");
        _fs.CreateDirectory(logsDir);
        var outPath = outputPath ?? Path.Combine(logsDir, $"session-{sessionId}.jsonl");

        await using var fs = _fs.CreateFile(outPath, FileMode.Create, FileAccess.Write, FileShare.Read);
        await using var sw = new StreamWriter(fs);
        var options = new System.Text.Json.JsonSerializerOptions { WriteIndented = false };

        int page = 0;
        while (true)
        {
            var rows = await _db.QueryLogsAsync(new LogQuery(SessionId: sessionId, Page: page, PageSize: 2000), ct);
            if (rows.Count == 0) break;
            foreach (var row in rows)
            {
                System.Text.Json.JsonElement? data = null;
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
                    operation_id = row.OperationId,
                    item_id = row.ItemId
                };
                var json = System.Text.Json.JsonSerializer.Serialize(line, options);
                await sw.WriteLineAsync(json);
            }
            page++;
            if (ct.IsCancellationRequested) break;
        }
        await sw.FlushAsync();
        return outPath;
    }

    public async Task ShutdownAsync(CancellationToken ct = default)
    {
        if (_writer is not null)
        {
            try { await _writer.FlushAsync(ct); } catch { }
        }
        if (_sessionId != 0)
        {
            try { await _db.EndSessionAsync(_sessionId, ct); }
            catch { }
            finally { _sessionId = 0; _currentOperationId = 0; }
        }
    }

    public Task AppendOrBufferAsync(LogWrite write, CancellationToken ct = default)
    {
        if (_sessionId == 0)
        {
            _pending.Enqueue(write);
            return Task.CompletedTask;
        }
        if (_writer != null)
        {
            return _writer.AppendAsync(write, ct);
        }
        return SafeAppendAsync(write, ct);
    }

    public async Task FlushAsync(CancellationToken ct = default)
    {
        if (_writer != null)
        {
            try { await _writer.FlushAsync(ct); } catch { }
        }
        else
        {
            await FlushPendingAsync(ct);
        }
    }

    // ILogAppender implementation forwards to runtime methods
    public Task AppendAsync(LogWrite write, CancellationToken ct = default) => AppendOrBufferAsync(write, ct);

    private async Task FlushPendingAsync(CancellationToken ct)
    {
        while (_pending.TryDequeue(out var w))
        {
            try { await _db.AppendLogAsync(w, ct); } catch { }
            if (ct.IsCancellationRequested) break;
        }
    }

    private async Task SafeAppendAsync(LogWrite write, CancellationToken ct)
    {
        try { await _db.AppendLogAsync(write, ct); } catch { }
    }

    public void Dispose()
    {
        (_db as IDisposable)?.Dispose();
    }

    private sealed class MaterializedLog
    {
        public long ts_ms { get; set; }
        public int level { get; set; }
        public string? category { get; set; }
        public string? subcategory { get; set; }
        public string? message { get; set; }
        public System.Text.Json.JsonElement? data { get; set; }
        public long? operation_id { get; set; }
        public long? item_id { get; set; }
    }

    private sealed class ChannelWriterImpl
    {
        private readonly Channel<LogWrite> _channel;
        private readonly IWorkspaceDb _db;
        private readonly Task _consumer;

        public ChannelWriterImpl(IWorkspaceDb db)
        {
            _db = db;
            _channel = Channel.CreateBounded<LogWrite>(new BoundedChannelOptions(8192)
            {
                SingleReader = true,
                SingleWriter = false,
                FullMode = BoundedChannelFullMode.DropOldest
            });
            _consumer = Task.Run(ConsumeAsync);
        }

        public async Task AppendAsync(LogWrite write, CancellationToken ct = default)
        {
            try { await _channel.Writer.WriteAsync(write, ct); } catch { }
        }

        public async Task FlushAsync(CancellationToken ct = default)
        {
            _channel.Writer.Complete();
            try { await _consumer; } catch { }
        }

        private async Task ConsumeAsync()
        {
            try
            {
                await foreach (var w in _channel.Reader.ReadAllAsync())
                {
                    try { await _db.AppendLogAsync(w); } catch { }
                }
            }
            catch { }
        }
    }
}
