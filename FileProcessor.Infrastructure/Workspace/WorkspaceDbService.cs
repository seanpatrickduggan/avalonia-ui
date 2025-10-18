using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FileProcessor.Core;
using FileProcessor.Core.Workspace;

namespace FileProcessor.Infrastructure.Workspace;

// Lightweight static facade to manage a workspace DB lifetime for the app.
public static class WorkspaceDbService
{
    private static IWorkspaceDb? _db;
    private static long _sessionId;
    private static long _currentRunId;

    public static IWorkspaceDb Db => _db ?? throw new InvalidOperationException("Workspace DB not initialized");
    public static long SessionId => _sessionId;
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
}
