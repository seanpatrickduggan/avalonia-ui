using FileProcessor.Core.Workspace;

namespace LogViewer.UI.Services;

internal sealed class FakeWorkspaceRuntime : IWorkspaceRuntime
{
    public long SessionId => 0;
    public long CurrentOperationId => 0;

    public Task EndOperationAsync(string status = "succeeded", long endedAtMs = 0, System.Threading.CancellationToken ct = default) => Task.CompletedTask;
    public Task<long> StartOperationAsync(string type, string? name = null, string? metadataJson = null, long startedAtMs = 0, System.Threading.CancellationToken ct = default) => Task.FromResult(0L);
    public Task InitializeAsync(System.Threading.CancellationToken ct = default) => Task.CompletedTask;
    public Task<string> MaterializeOperationLogsAsync(long operationId, string? outputPath = null, System.Threading.CancellationToken ct = default) => Task.FromResult(string.Empty);
    public Task<string> MaterializeSessionLogsAsync(long sessionId, string? outputPath = null, System.Threading.CancellationToken ct = default) => Task.FromResult(string.Empty);
    public Task ShutdownAsync(System.Threading.CancellationToken ct = default) => Task.CompletedTask;
    public Task AppendOrBufferAsync(FileProcessor.Core.Workspace.LogWrite write, System.Threading.CancellationToken ct = default) => Task.CompletedTask;
    public Task FlushAsync(System.Threading.CancellationToken ct = default) => Task.CompletedTask;
}
