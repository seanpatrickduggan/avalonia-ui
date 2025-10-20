using System.Threading;
using System.Threading.Tasks;
using FileProcessor.Core.App;
using FileProcessor.Core.Workspace;
using FileProcessor.Infrastructure.App;
using FluentAssertions;
using Xunit;

namespace FileProcessor.Infrastructure.Tests.App;

public class ApplicationHostTests
{
    private sealed class FakeWorkspaceRuntime : IWorkspaceRuntime
    {
        public bool InitializeCalled { get; private set; }
        public bool FlushCalled { get; private set; }
        public bool ShutdownCalled { get; private set; }
        public Exception? FlushException { get; set; }
        public Exception? ShutdownException { get; set; }

        public long SessionId => 1;
        public long CurrentOperationId => 1;

        public Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            InitializeCalled = true;
            return Task.CompletedTask;
        }

        public Task<long> StartOperationAsync(string type, string? name = null, string? metadataJson = null, long startedAtMs = 0, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(1L);
        }

        public Task EndOperationAsync(string status = "succeeded", long endedAtMs = 0, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task<string> MaterializeOperationLogsAsync(long operationId, string? outputPath = null, CancellationToken cancellationToken = default)
        {
            return Task.FromResult("logs");
        }

        public Task<string> MaterializeSessionLogsAsync(long sessionId, string? outputPath = null, CancellationToken cancellationToken = default)
        {
            return Task.FromResult("logs");
        }

        public Task ShutdownAsync(CancellationToken cancellationToken = default)
        {
            ShutdownCalled = true;
            if (ShutdownException != null)
                throw ShutdownException;
            return Task.CompletedTask;
        }

        public Task AppendOrBufferAsync(LogWrite write, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task FlushAsync(CancellationToken cancellationToken = default)
        {
            FlushCalled = true;
            if (FlushException != null)
                throw FlushException;
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task InitializeAsync_CallsWorkspaceInitialize()
    {
        var fakeWorkspace = new FakeWorkspaceRuntime();
        var host = new ApplicationHost(fakeWorkspace);

        await host.InitializeAsync();

        fakeWorkspace.InitializeCalled.Should().BeTrue();
    }

    [Fact]
    public async Task ShutdownAsync_CallsWorkspaceFlushAndShutdown()
    {
        var fakeWorkspace = new FakeWorkspaceRuntime();
        var host = new ApplicationHost(fakeWorkspace);

        await host.ShutdownAsync();

        fakeWorkspace.FlushCalled.Should().BeTrue();
        fakeWorkspace.ShutdownCalled.Should().BeTrue();
    }

    [Fact]
    public async Task ShutdownAsync_HandlesFlushException()
    {
        var fakeWorkspace = new FakeWorkspaceRuntime
        {
            FlushException = new System.Exception("Flush failed")
        };
        var host = new ApplicationHost(fakeWorkspace);

        // Should not throw
        await host.ShutdownAsync();

        fakeWorkspace.FlushCalled.Should().BeTrue();
        fakeWorkspace.ShutdownCalled.Should().BeTrue();
    }

    [Fact]
    public async Task ShutdownAsync_HandlesShutdownException()
    {
        var fakeWorkspace = new FakeWorkspaceRuntime
        {
            ShutdownException = new System.Exception("Shutdown failed")
        };
        var host = new ApplicationHost(fakeWorkspace);

        // Should not throw
        await host.ShutdownAsync();

        fakeWorkspace.FlushCalled.Should().BeTrue();
        fakeWorkspace.ShutdownCalled.Should().BeTrue();
    }
}
