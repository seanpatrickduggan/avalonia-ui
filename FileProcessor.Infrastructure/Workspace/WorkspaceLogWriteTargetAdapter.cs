using FileProcessor.Infrastructure.Logging;
using FileProcessor.Core.Workspace;

namespace FileProcessor.Infrastructure.Workspace;

public sealed class WorkspaceLogWriteTargetAdapter : ILogWriteTarget
{
    public long? SessionIdOrNull => WorkspaceDbService.SessionIdOrNull;
    public long CurrentOperationId => WorkspaceDbService.CurrentOperationId;
    public void AppendOrBuffer(LogWrite write) => WorkspaceDbService.AppendOrBuffer(write);
}
