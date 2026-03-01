using FileProcessor.Core.App;
using FileProcessor.Core.Workspace;
using Serilog;

namespace FileProcessor.Infrastructure.App;

public sealed class ApplicationHost : IApplicationHost
{
    private readonly IWorkspaceRuntime _workspace;

    public ApplicationHost(IWorkspaceRuntime workspace)
    {
        _workspace = workspace;
    }

    public Task InitializeAsync(CancellationToken cancellationToken = default)
        => _workspace.InitializeAsync(cancellationToken);

    public async Task ShutdownAsync(CancellationToken cancellationToken = default)
    {
        try { await _workspace.FlushAsync(cancellationToken); }
        catch (Exception ex) { Log.Debug(ex, "Failed to flush workspace during shutdown"); }
        try { await _workspace.ShutdownAsync(cancellationToken); }
        catch (Exception ex) { Log.Debug(ex, "Failed to shutdown workspace"); }
    }
}
