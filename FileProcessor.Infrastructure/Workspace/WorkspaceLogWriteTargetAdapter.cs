using FileProcessor.Infrastructure.Logging;
using FileProcessor.Core.Workspace;

namespace FileProcessor.Infrastructure.Workspace;

public interface ILogAppender
{
    System.Threading.Tasks.Task AppendAsync(LogWrite write, System.Threading.CancellationToken ct = default);
    System.Threading.Tasks.Task FlushAsync(System.Threading.CancellationToken ct = default);
}

public sealed class WorkspaceLogWriteTarget : ILogWriteTarget
{
    private readonly IWorkspaceRuntime _runtime;
    private readonly ILogAppender _appender;

    public WorkspaceLogWriteTarget(IWorkspaceRuntime runtime, ILogAppender appender)
    {
        _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        _appender = appender ?? throw new ArgumentNullException(nameof(appender));
    }

    public long? SessionIdOrNull => _runtime.SessionId == 0 ? (long?)null : _runtime.SessionId;
    public long CurrentOperationId => _runtime.CurrentOperationId;

    public void AppendOrBuffer(LogWrite write)
    {
        // Append asynchronously via bounded writer on the instance runtime
        _ = _appender.AppendAsync(write);
    }
}
