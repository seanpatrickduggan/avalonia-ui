using System.Collections.Concurrent;

using FileProcessor.Core.Abstractions;
using FileProcessor.Core.Interfaces;
using FileProcessor.Core.Workspace;
using FileProcessor.Infrastructure.Abstractions;
using FileProcessor.Infrastructure.Workspace;

using FluentAssertions;

using Xunit;

namespace FileProcessor.Infrastructure.Tests;

public class WorkspaceRuntimeTests
{
    [Fact]
    public async Task Buffered_writes_are_flushed_on_initialize()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "wr-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var db = new FakeDb();
            var settings = new FakeSettingsService { WorkspaceDirectory = tempDir };
            IFileSystem fs = new SystemFileSystem();
            var rt = new WorkspaceRuntime(db, settings, fs);

            // enqueue before init (will buffer)
            await rt.AppendOrBufferAsync(new LogWrite(1, 2, "c", "s", "m1", null, null, null, null, null));
            await rt.AppendOrBufferAsync(new LogWrite(2, 2, "c", "s", "m2", null, null, null, null, null));
            await rt.AppendOrBufferAsync(new LogWrite(3, 2, "c", "s", "m3", null, null, null, null, null));

            await rt.InitializeAsync();

            // pending should be flushed during init
            db.AppendCount.Should().Be(3);
        }
        finally
        {
            try { Directory.Delete(tempDir, recursive: true); } catch { }
        }
    }

    [Fact]
    public async Task Channel_writer_flushes_on_FlushAsync()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "wr-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var db = new FakeDb();
            var settings = new FakeSettingsService { WorkspaceDirectory = tempDir };
            IFileSystem fs = new SystemFileSystem();
            var rt = new WorkspaceRuntime(db, settings, fs);
            await rt.InitializeAsync();

            var before = db.AppendCount;
            for (int i = 0; i < 5; i++)
                await rt.AppendOrBufferAsync(new LogWrite(100 + i, 2, "c", "s", "mx", null, null, null, null, null));

            await rt.FlushAsync();
            db.AppendCount.Should().Be(before + 5);
        }
        finally
        {
            try { Directory.Delete(tempDir, recursive: true); } catch { }
        }
    }

    [Fact]
    public async Task StartOperationAsync_sets_current_operation_id()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "wr-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var db = new FakeDb();
            var settings = new FakeSettingsService { WorkspaceDirectory = tempDir };
            IFileSystem fs = new SystemFileSystem();
            var rt = new WorkspaceRuntime(db, settings, fs);
            await rt.InitializeAsync();

            rt.CurrentOperationId.Should().Be(0);
            var opId = await rt.StartOperationAsync("test-op", "test name");
            opId.Should().Be(11L);
            rt.CurrentOperationId.Should().Be(11L);

            // Also test EndOperationAsync
            await rt.EndOperationAsync();
            rt.CurrentOperationId.Should().Be(0);
        }
        finally
        {
            try { Directory.Delete(tempDir, recursive: true); } catch { }
        }
    }

    [Fact]
    public async Task ShutdownAsync_ends_session_and_clears_ids()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "wr-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var db = new FakeDb();
            var settings = new FakeSettingsService { WorkspaceDirectory = tempDir };
            IFileSystem fs = new SystemFileSystem();
            var rt = new WorkspaceRuntime(db, settings, fs);
            await rt.InitializeAsync();

            rt.SessionId.Should().Be(1L);
            await rt.StartOperationAsync("test-op");
            rt.CurrentOperationId.Should().Be(11L);

            await rt.ShutdownAsync();
            rt.SessionId.Should().Be(0);
            rt.CurrentOperationId.Should().Be(0);
        }
        finally
        {
            try { Directory.Delete(tempDir, recursive: true); } catch { }
        }
    }

    [Fact]
    public async Task Dispose_calls_db_Dispose()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "wr-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var db = new FakeDb();
            var settings = new FakeSettingsService { WorkspaceDirectory = tempDir };
            IFileSystem fs = new SystemFileSystem();
            var rt = new WorkspaceRuntime(db, settings, fs);

            rt.Dispose();
            db.DisposeCalled.Should().BeTrue();
        }
        finally
        {
            try { Directory.Delete(tempDir, recursive: true); } catch { }
        }
    }

    [Fact]
    public async Task AppendAsync_forwards_to_AppendOrBufferAsync()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "wr-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var db = new FakeDb();
            var settings = new FakeSettingsService { WorkspaceDirectory = tempDir };
            IFileSystem fs = new SystemFileSystem();
            var rt = new WorkspaceRuntime(db, settings, fs);
            await rt.InitializeAsync();

            var before = db.AppendCount;
            await ((ILogAppender)rt).AppendAsync(new LogWrite(100, 2, "c", "s", "m", null, null, null, null, null));

            // Flush to ensure the channel writer processes the queued item
            await rt.FlushAsync();

            db.AppendCount.Should().Be(before + 1);
        }
        finally
        {
            try { Directory.Delete(tempDir, recursive: true); } catch { }
        }
    }

    [Fact]
    public async Task SafeAppendAsync_called_when_writer_is_null()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "wr-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var db = new FakeDb();
            var settings = new FakeSettingsService { WorkspaceDirectory = tempDir };
            IFileSystem fs = new SystemFileSystem();
            var rt = new WorkspaceRuntime(db, settings, fs);
            await rt.InitializeAsync();

            // Use reflection to set _writer to null to test SafeAppendAsync
            var writerField = typeof(WorkspaceRuntime).GetField("_writer", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            writerField?.SetValue(rt, null);

            var before = db.AppendCount;
            await rt.AppendOrBufferAsync(new LogWrite(100, 2, "c", "s", "m", null, null, null, null, null));
            db.AppendCount.Should().Be(before + 1);
        }
        finally
        {
            try { Directory.Delete(tempDir, recursive: true); } catch { }
        }
    }

    private sealed class FakeDb : IWorkspaceDb
    {
        public int AppendCount => _appends.Count;
        private readonly ConcurrentQueue<LogWrite> _appends = new();
        public bool DisposeCalled { get; private set; }
        public void Dispose() { DisposeCalled = true; }
        public Task InitializeAsync(string dbPath, CancellationToken ct = default) => Task.CompletedTask;
        public Task<long> StartSessionAsync(string? appVersion = null, string? userName = null, string? hostName = null, CancellationToken ct = default) => Task.FromResult(1L);
        public Task EndSessionAsync(long sessionId, CancellationToken ct = default) => Task.CompletedTask;
        public Task<long> StartOperationAsync(long sessionId, string type, string? name = null, string? metadataJson = null, long startedAtMs = 0, CancellationToken ct = default) => Task.FromResult(11L);
        public Task EndOperationAsync(long operationId, string status, long endedAtMs = 0, CancellationToken ct = default) => Task.CompletedTask;
        public Task<long> UpsertItemAsync(long operationId, string externalId, string status, int highestSeverity = 2, long startedAtMs = 0, long endedAtMs = 0, string? metricsJson = null, CancellationToken ct = default) => Task.FromResult(0L);
        public Task<long> AppendLogAsync(LogWrite log, CancellationToken ct = default)
        {
            _appends.Enqueue(log);
            return Task.FromResult((long)_appends.Count);
        }
        public Task<IReadOnlyList<LogRow>> QueryLogsAsync(LogQuery q, CancellationToken ct = default) => Task.FromResult((IReadOnlyList<LogRow>)Array.Empty<LogRow>());
        public Task<IReadOnlyList<LogGroupCount>> QueryGroupCountsAsync(LogQuery q, CancellationToken ct = default) => Task.FromResult((IReadOnlyList<LogGroupCount>)Array.Empty<LogGroupCount>());
    }

    private sealed class FakeSettingsService : ISettingsService
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
        public Task<bool> AddWorkspaceAsync(string workspacePath, string? name = null)
        {
            Directory.CreateDirectory(workspacePath);
            Workspaces.Add(new WorkspaceInfo { Path = workspacePath, Name = name ?? Path.GetFileName(workspacePath), IsActive = false });
            return Task.FromResult(true);
        }
        public Task<bool> SetActiveWorkspaceAsync(string workspacePath)
        {
            WorkspaceDirectory = workspacePath;
            for (int i = 0; i < Workspaces.Count; i++) Workspaces[i].IsActive = Workspaces[i].Path == workspacePath;
            WorkspaceChanged?.Invoke(this, WorkspaceDirectory);
            return Task.FromResult(true);
        }
        public Task SetActiveWorkspaceAsync(WorkspaceInfo workspace)
        {
            WorkspaceDirectory = workspace.Path;
            for (int i = 0; i < Workspaces.Count; i++) Workspaces[i].IsActive = Workspaces[i].Path == workspace.Path;
            WorkspaceChanged?.Invoke(this, WorkspaceDirectory);
            return Task.CompletedTask;
        }
        public Task<bool> RemoveWorkspaceAsync(string workspacePath)
        {
            Workspaces.RemoveAll(w => w.Path == workspacePath);
            if (WorkspaceDirectory == workspacePath) WorkspaceDirectory = null;
            return Task.FromResult(true);
        }
        public Task RemoveWorkspaceAsync(WorkspaceInfo workspace)
        {
            Workspaces.RemoveAll(w => w.Path == workspace.Path);
            if (WorkspaceDirectory == workspace.Path) WorkspaceDirectory = null;
            return Task.CompletedTask;
        }
    }
}
