using System.Threading.Tasks;
using FileProcessor.Core.Workspace;
using FileProcessor.Infrastructure.Workspace;
using FluentAssertions;
using Xunit;

namespace FileProcessor.Infrastructure.Tests;

public class WorkspaceLogWriteTargetTests
{
    [Fact]
    public void Constructor_ThrowsOnNullRuntime()
    {
        var appender = new FakeLogAppender();
        Action act = () => new WorkspaceLogWriteTarget(null!, appender);
        act.Should().Throw<ArgumentNullException>().WithParameterName("runtime");
    }

    [Fact]
    public void Constructor_ThrowsOnNullAppender()
    {
        var runtime = new FakeWorkspaceRuntime();
        Action act = () => new WorkspaceLogWriteTarget(runtime, null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("appender");
    }

    [Fact]
    public void SessionIdOrNull_ReturnsNull_WhenSessionIdIsZero()
    {
        var runtime = new FakeWorkspaceRuntime { SessionId = 0 };
        var appender = new FakeLogAppender();
        var target = new WorkspaceLogWriteTarget(runtime, appender);

        target.SessionIdOrNull.Should().BeNull();
    }

    [Fact]
    public void SessionIdOrNull_ReturnsSessionId_WhenSessionIdIsNonZero()
    {
        var runtime = new FakeWorkspaceRuntime { SessionId = 123L };
        var appender = new FakeLogAppender();
        var target = new WorkspaceLogWriteTarget(runtime, appender);

        target.SessionIdOrNull.Should().Be(123L);
    }

    [Fact]
    public void CurrentOperationId_ReturnsRuntimeCurrentOperationId()
    {
        var runtime = new FakeWorkspaceRuntime { CurrentOperationId = 456L };
        var appender = new FakeLogAppender();
        var target = new WorkspaceLogWriteTarget(runtime, appender);

        target.CurrentOperationId.Should().Be(456L);
    }

    [Fact]
    public void AppendOrBuffer_CallsAppenderAppendAsync()
    {
        var runtime = new FakeWorkspaceRuntime();
        var appender = new FakeLogAppender();
        var target = new WorkspaceLogWriteTarget(runtime, appender);

        var logWrite = new LogWrite(1, 2, "cat", "sub", "msg", "data", 10, 20, 30, "src");

        target.AppendOrBuffer(logWrite);

        appender.Appends.Should().HaveCount(1);
        appender.Appends[0].Should().Be(logWrite);
    }

    private sealed class FakeWorkspaceRuntime : IWorkspaceRuntime
    {
        public long SessionId { get; set; } = 1L;
        public long CurrentOperationId { get; set; } = 1L;
        public Task InitializeAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task<long> StartOperationAsync(string type, string? name = null, string? metadataJson = null, long startedAtMs = 0, CancellationToken ct = default) => Task.FromResult(1L);
        public Task EndOperationAsync(string status = "succeeded", long endedAtMs = 0, CancellationToken ct = default) => Task.CompletedTask;
        public Task<string> MaterializeOperationLogsAsync(long operationId, string? outputPath = null, CancellationToken ct = default) => Task.FromResult("logs");
        public Task<string> MaterializeSessionLogsAsync(long sessionId, string? outputPath = null, CancellationToken ct = default) => Task.FromResult("logs");
        public Task ShutdownAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task AppendOrBufferAsync(LogWrite write, CancellationToken ct = default) => Task.CompletedTask;
        public Task FlushAsync(CancellationToken ct = default) => Task.CompletedTask;
        public void Dispose() { }
    }

    private sealed class FakeLogAppender : ILogAppender
    {
        public List<LogWrite> Appends { get; } = new();
        public Task AppendAsync(LogWrite write, CancellationToken ct = default)
        {
            Appends.Add(write);
            return Task.CompletedTask;
        }
        public Task FlushAsync(CancellationToken ct = default) => Task.CompletedTask;
    }
}
