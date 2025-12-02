using System.Text.Json;

using FileProcessor.Core.Abstractions;
using FileProcessor.Core.Interfaces;
using FileProcessor.Core.Workspace;
using FileProcessor.Infrastructure.Abstractions;
using FileProcessor.Infrastructure.Workspace;

using FluentAssertions;

using Xunit;

namespace FileProcessor.Infrastructure.Tests;

public class WorkspaceRuntimeMaterializeTests
{
    [Fact]
    public async Task MaterializeOperationLogs_Writes_Jsonl()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "wr-mat-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var fakeDb = new MatDb();
            var settings = new MatSettings { WorkspaceDirectory = tempDir };
            IFileSystem fs = new SystemFileSystem();
            var rt = new WorkspaceRuntime(fakeDb, settings, fs);

            await rt.InitializeAsync();

            // Prepare rows in fake DB for operation 99
            fakeDb.RowsByOp[99] = new List<LogRow>
            {
                new LogRow(1, 1000, 2, "core", "a", "m1", null, 99, null),
                new LogRow(2, 1001, 3, "core", "b", "m2", JsonSerializer.Serialize(new { x = 1 }), 99, 7),
                new LogRow(3, 1002, 4, null, null, null, null, 99, null)
            };

            var outPath = await rt.MaterializeOperationLogsAsync(99, ct: CancellationToken.None);
            File.Exists(outPath).Should().BeTrue();
            var lines = await File.ReadAllLinesAsync(outPath);
            lines.Length.Should().Be(3);
            lines.Any(l => l.Contains("\"message\":\"m1\"", StringComparison.Ordinal)).Should().BeTrue();
            lines.Any(l => l.Contains("\"message\":\"m2\"", StringComparison.Ordinal)).Should().BeTrue();
        }
        finally
        {
            try { Directory.Delete(tempDir, recursive: true); } catch { }
        }
    }

    [Fact]
    public async Task MaterializeSessionLogs_Writes_Jsonl()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "wr-mat-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var fakeDb = new MatDb();
            var settings = new MatSettings { WorkspaceDirectory = tempDir };
            IFileSystem fs = new SystemFileSystem();
            var rt = new WorkspaceRuntime(fakeDb, settings, fs);

            await rt.InitializeAsync();

            fakeDb.RowsBySession[5] = new List<LogRow>
            {
                new LogRow(1, 2000, 2, "core", "x", "s1", null, 91, null),
                new LogRow(2, 2001, 2, "core", "y", "s2", null, 92, null)
            };

            var outPath = await rt.MaterializeSessionLogsAsync(5, ct: CancellationToken.None);
            File.Exists(outPath).Should().BeTrue();
            var lines = await File.ReadAllLinesAsync(outPath);
            lines.Length.Should().Be(2);
            lines.Any(l => l.Contains("\"message\":\"s1\"", StringComparison.Ordinal)).Should().BeTrue();
            lines.Any(l => l.Contains("\"message\":\"s2\"", StringComparison.Ordinal)).Should().BeTrue();
        }
        finally
        {
            try { Directory.Delete(tempDir, recursive: true); } catch { }
        }
    }

    private sealed class MatDb : IWorkspaceDb
    {
        public Dictionary<long, List<LogRow>> RowsByOp { get; } = new();
        public Dictionary<long, List<LogRow>> RowsBySession { get; } = new();
        public void Dispose() { }
        public Task InitializeAsync(string dbPath, CancellationToken ct = default) => Task.CompletedTask;
        public Task<long> StartSessionAsync(string? appVersion = null, string? userName = null, string? hostName = null, CancellationToken ct = default) => Task.FromResult(1L);
        public Task EndSessionAsync(long sessionId, CancellationToken ct = default) => Task.CompletedTask;
        public Task<long> StartOperationAsync(long sessionId, string type, string? name = null, string? metadataJson = null, long startedAtMs = 0, CancellationToken ct = default) => Task.FromResult(99L);
        public Task EndOperationAsync(long operationId, string status, long endedAtMs = 0, CancellationToken ct = default) => Task.CompletedTask;
        public Task<long> UpsertItemAsync(long operationId, string externalId, string status, int highestSeverity = 2, long startedAtMs = 0, long endedAtMs = 0, string? metricsJson = null, CancellationToken ct = default) => Task.FromResult(0L);
        public Task<long> AppendLogAsync(LogWrite log, CancellationToken ct = default) => Task.FromResult(0L);
        public Task<IReadOnlyList<LogRow>> QueryLogsAsync(LogQuery q, CancellationToken ct = default)
        {
            // Respect simple paging: only return rows for page 0; subsequent pages empty
            if (q.Page > 0) return Task.FromResult((IReadOnlyList<LogRow>)Array.Empty<LogRow>());

            if (q.OperationId is long oid)
            {
                if (RowsByOp.TryGetValue(oid, out var list))
                    return Task.FromResult((IReadOnlyList<LogRow>)list);
                return Task.FromResult((IReadOnlyList<LogRow>)Array.Empty<LogRow>());
            }
            if (q.SessionId is long sid)
            {
                if (RowsBySession.TryGetValue(sid, out var list))
                    return Task.FromResult((IReadOnlyList<LogRow>)list);
                return Task.FromResult((IReadOnlyList<LogRow>)Array.Empty<LogRow>());
            }
            return Task.FromResult((IReadOnlyList<LogRow>)Array.Empty<LogRow>());
        }
        public Task<IReadOnlyList<LogGroupCount>> QueryGroupCountsAsync(LogQuery q, CancellationToken ct = default)
            => Task.FromResult((IReadOnlyList<LogGroupCount>)Array.Empty<LogGroupCount>());
    }

    private sealed class MatSettings : ISettingsService
    {
        public string? WorkspaceDirectory { get; set; }
        public List<WorkspaceInfo> Workspaces { get; } = new();
        public string? InputDirectory => WorkspaceDirectory is null ? null : Path.Combine(WorkspaceDirectory, "input");
        public string? ProcessedDirectory => WorkspaceDirectory is null ? null : Path.Combine(WorkspaceDirectory, "processed");
        public int CoreSpareCount { get; set; } = 1;
        public int MaxDegreeOfParallelism => Math.Max(1, Environment.ProcessorCount - CoreSpareCount);
        public event EventHandler<string?>? WorkspaceChanged;
        public Task SaveSettingsAsync() => Task.CompletedTask;
        public Task LoadSettingsAsync() => Task.CompletedTask;
        public Task<bool> AddWorkspaceAsync(string workspacePath, string? name = null) => Task.FromResult(true);
        public Task<bool> SetActiveWorkspaceAsync(string workspacePath) { WorkspaceDirectory = workspacePath; WorkspaceChanged?.Invoke(this, WorkspaceDirectory); return Task.FromResult(true); }
        public Task SetActiveWorkspaceAsync(WorkspaceInfo workspace) { WorkspaceDirectory = workspace.Path; WorkspaceChanged?.Invoke(this, WorkspaceDirectory); return Task.CompletedTask; }
        public Task<bool> RemoveWorkspaceAsync(string workspacePath) => Task.FromResult(true);
        public Task RemoveWorkspaceAsync(WorkspaceInfo workspace) => Task.CompletedTask;
    }
}
