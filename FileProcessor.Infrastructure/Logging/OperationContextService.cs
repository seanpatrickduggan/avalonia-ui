using System;
using System.IO;
using System.Threading.Tasks;
using FileProcessor.Core.Logging;
using FileProcessor.Core.Workspace;
using Serilog;

namespace FileProcessor.Infrastructure.Logging;

// Non-static implementation of IOperationContext.
// Encapsulates operation lifecycle (start/end) and logging setup.
public sealed class OperationContextService : IOperationContext
{
    private ILogger? _rootLogger;
    private readonly IWorkspaceRuntime _runtime;
    private readonly ILogWriteTarget _target;

    public OperationContextService(IWorkspaceRuntime runtime, ILogWriteTarget target)
    {
        _runtime = runtime;
        _target = target;
    }

    public string OperationId { get; private set; } = string.Empty;
    public string LogFilePath { get; private set; } = string.Empty;
    public IItemLogFactory ItemLogFactory { get; private set; } = default!;
    public IOperationStructuredLogger OperationLogger { get; private set; } = default!;

    // Core no longer sees Serilog.ILogger; Infra owns logger creation.
    public void Initialize(string operationId, string logFilePath)
    {
        // If an external logger already exists, keep it; otherwise build a basic one for current operation.
        if (_rootLogger == null)
        {
            _rootLogger = new LoggerConfiguration()
                .Enrich.WithProperty("app", "FileProcessor")
                .Enrich.WithProperty("operation", operationId)
                .MinimumLevel.Debug()
                .WriteTo.Async(a => a.File(
                    path: logFilePath,
                    shared: false,
                    formatter: new Serilog.Formatting.Compact.CompactJsonFormatter()))
                .WriteTo.Sink(new WorkspaceSqliteSink(_target))
                .CreateLogger();
        }
        OperationLogger = new WorkspaceOperationStructuredLogger(_rootLogger);
        ApplyOperation(operationId, logFilePath);
    }

    private void ApplyOperation(string operationId, string logFilePath)
    {
        OperationId = operationId;
        LogFilePath = logFilePath;
        var options = new ItemLogOptions();
        ItemLogFactory = new ItemLogFactory(options, OperationLogger, () => Guid.Empty, () => null);
    }

    public async Task StartNewOperationAsync(string? operationType = null)
    {
        try { Log.CloseAndFlush(); } catch { }

        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
        var opId = string.IsNullOrEmpty(operationType) ? timestamp : $"{operationType}_{timestamp}";

        var workspacePath = FileProcessor.Core.SettingsService.Instance.WorkspaceDirectory;
        if (string.IsNullOrWhiteSpace(workspacePath))
            throw new InvalidOperationException("No workspace configured. Please configure a workspace before starting a new operation.");

        var logsDir = Path.Combine(workspacePath, "logs");
        Directory.CreateDirectory(logsDir);
        var newPath = Path.Combine(logsDir, $"operation-{opId}.jsonl");

        _rootLogger = new LoggerConfiguration()
            .Enrich.WithProperty("app", "FileProcessor")
            .Enrich.WithProperty("operation", opId)
            .MinimumLevel.Debug()
            .WriteTo.Async(a => a.File(
                path: newPath,
                shared: false,
                formatter: new Serilog.Formatting.Compact.CompactJsonFormatter()))
            .WriteTo.Sink(new WorkspaceSqliteSink(_target))
            .CreateLogger();

        OperationLogger = new WorkspaceOperationStructuredLogger(_rootLogger);
        ApplyOperation(opId, newPath);

        try { await _runtime.StartOperationAsync(operationType ?? "operation", name: opId); } catch { }
    }

    public async Task EndCurrentOperationAsync(string status = "succeeded")
    {
        try
        {
            var rid = _runtime.CurrentOperationId;
            await _runtime.EndOperationAsync(status: status);
            if (rid != 0)
            {
                try { await _runtime.MaterializeOperationLogsAsync(rid); } catch { }
            }
        }
        catch { }
    }
}
