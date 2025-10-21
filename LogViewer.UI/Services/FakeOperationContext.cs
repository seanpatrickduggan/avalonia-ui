using FileProcessor.Core.Logging;
using System.Threading.Tasks;

namespace LogViewer.UI.Services;

internal sealed class FakeOperationContext : IOperationContext
{
    public string OperationId { get; private set; } = string.Empty;
    public string LogFilePath { get; private set; } = string.Empty;
    public IItemLogFactory ItemLogFactory => throw new System.NotImplementedException();
    public IOperationStructuredLogger OperationLogger => throw new System.NotImplementedException();

    public void Initialize(string operationId, string logFilePath)
    {
        OperationId = operationId ?? string.Empty;
        LogFilePath = logFilePath ?? string.Empty;
    }

    public Task StartNewOperationAsync(string? operationType = null) => Task.CompletedTask;
    public Task EndCurrentOperationAsync(string status = "succeeded") => Task.CompletedTask;
}
