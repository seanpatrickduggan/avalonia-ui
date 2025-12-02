#pragma warning disable CS0067
using FileProcessor.Core.Abstractions;
using FileProcessor.Core.Interfaces;
using FileProcessor.Core.Workspace;
using FileProcessor.Infrastructure.Abstractions;
using FileProcessor.Infrastructure.Logging;

using FluentAssertions;

using Microsoft.Extensions.Time.Testing;

using Xunit;

namespace FileProcessor.Infrastructure.Tests;

public class OperationContextServiceTests
{
    private sealed class FakeWorkspaceRuntime : IWorkspaceRuntime
    {
        public long SessionId { get; set; }
        public long CurrentOperationId { get; set; }
        public string? StartedOperationType { get; set; }
        public string? StartedOperationName { get; set; }
        public string? EndedStatus { get; set; }
        public long MaterializedOperationId { get; set; }

        public Task InitializeAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task<long> StartOperationAsync(string type, string? name = null, string? metadataJson = null, long startedAtMs = 0, CancellationToken ct = default)
        {
            StartedOperationType = type;
            StartedOperationName = name;
            CurrentOperationId = 42;
            return Task.FromResult(42L);
        }
        public Task EndOperationAsync(string status = "succeeded", long endedAtMs = 0, CancellationToken ct = default)
        {
            EndedStatus = status;
            return Task.CompletedTask;
        }
        public Task<string> MaterializeOperationLogsAsync(long operationId, string? outputPath = null, CancellationToken ct = default)
        {
            MaterializedOperationId = operationId;
            return Task.FromResult("output.jsonl");
        }
        public Task<string> MaterializeSessionLogsAsync(long sessionId, string? outputPath = null, CancellationToken ct = default) => Task.FromResult("session.jsonl");
        public Task ShutdownAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task AppendOrBufferAsync(LogWrite write, CancellationToken ct = default) => Task.CompletedTask;
        public Task FlushAsync(CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class FakeLogWriteTarget : ILogWriteTarget
    {
        public long? SessionIdOrNull { get; set; }
        public long CurrentOperationId { get; set; }
        public void AppendOrBuffer(LogWrite write) { }
    }

    private sealed class FakeSettingsService : ISettingsService
    {
        public string? WorkspaceDirectory { get; set; }
        public List<WorkspaceInfo> Workspaces => new();
        public string? InputDirectory => null;
        public string? ProcessedDirectory => null;
        public int CoreSpareCount { get; set; }
        public int MaxDegreeOfParallelism => 1;
        public event EventHandler<string?>? WorkspaceChanged;
        public Task SaveSettingsAsync() => Task.CompletedTask;
        public Task LoadSettingsAsync() => Task.CompletedTask;
        public Task<bool> AddWorkspaceAsync(string workspacePath, string? name = null) => Task.FromResult(true);
        public Task<bool> SetActiveWorkspaceAsync(string workspacePath) => Task.FromResult(true);
        public Task SetActiveWorkspaceAsync(WorkspaceInfo workspace) => Task.CompletedTask;
        public Task<bool> RemoveWorkspaceAsync(string workspacePath) => Task.FromResult(true);
        public Task RemoveWorkspaceAsync(WorkspaceInfo workspace) => Task.CompletedTask;
    }

    private sealed class FakeFileSystem : IFileSystem
    {
        public void CreateDirectory(string path) { }
        public bool FileExists(string path) => false;
        public Stream CreateFile(string path, FileMode mode, FileAccess access, FileShare share) => Stream.Null;
        public Task WriteAllTextAsync(string path, string contents, CancellationToken ct = default) => Task.CompletedTask;
        public Task<string> ReadAllTextAsync(string path, CancellationToken ct = default) => Task.FromResult("");
        public void DeleteFile(string path) { }
    }

    [Fact]
    public void Initialize_Sets_Session_Logger_And_Factory()
    {
        var runtime = new FakeWorkspaceRuntime();
        var target = new FakeLogWriteTarget();
        var settings = new FakeSettingsService();
        var time = new FakeTimeProvider();
        var fs = new FakeFileSystem();
        var svc = new OperationContextService(runtime, target, settings, time, fs);

        svc.Initialize("session123", "log.jsonl");

        svc.OperationId.Should().Be("session123");
        svc.LogFilePath.Should().Be("log.jsonl");
        svc.OperationLogger.Should().NotBeNull();
        svc.ItemLogFactory.Should().NotBeNull();
    }

    [Fact]
    public async Task StartNewOperationAsync_Sets_Operation_And_Loggers()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "ocs-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var runtime = new FakeWorkspaceRuntime();
            var target = new FakeLogWriteTarget();
            var settings = new FakeSettingsService { WorkspaceDirectory = tempDir };
            var time = new FakeTimeProvider();
            var fs = new SystemFileSystem();
            var svc = new OperationContextService(runtime, target, settings, time, fs);

            svc.Initialize("session123", "log.jsonl");

            await svc.StartNewOperationAsync("test-op");

            svc.OperationLogger.Should().NotBeNull();
            svc.ItemLogFactory.Should().NotBeNull();
            runtime.StartedOperationType.Should().Be("test-op");
            runtime.StartedOperationName.Should().NotBeNull();
            Directory.Exists(Path.Combine(tempDir, "logs")).Should().BeTrue();
        }
        finally
        {
            try { Directory.Delete(tempDir, recursive: true); } catch { }
        }
    }

    [Fact]
    public async Task EndCurrentOperationAsync_Ends_Operation_And_Materializes()
    {
        var runtime = new FakeWorkspaceRuntime();
        var target = new FakeLogWriteTarget();
        var settings = new FakeSettingsService { WorkspaceDirectory = "/tmp" };
        var time = new FakeTimeProvider();
        var fs = new FakeFileSystem();
        var svc = new OperationContextService(runtime, target, settings, time, fs);

        svc.Initialize("session123", "log.jsonl");

        // Start an operation first
        await svc.StartNewOperationAsync("test-op");

        await svc.EndCurrentOperationAsync("completed");

        runtime.EndedStatus.Should().Be("completed");
    }

    [Fact]
    public async Task StartNewOperationAsync_Throws_If_No_Workspace()
    {
        var runtime = new FakeWorkspaceRuntime();
        var target = new FakeLogWriteTarget();
        var settings = new FakeSettingsService { WorkspaceDirectory = null };
        var time = new FakeTimeProvider();
        var fs = new FakeFileSystem();
        var svc = new OperationContextService(runtime, target, settings, time, fs);

        svc.Initialize("session123", "log.jsonl");

        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.StartNewOperationAsync("test-op"));
    }
}

#pragma warning restore CS0067
