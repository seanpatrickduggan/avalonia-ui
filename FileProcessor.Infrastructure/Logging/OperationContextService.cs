using FileProcessor.Core.Logging;
using FileProcessor.Core.Workspace;
using Serilog;
using FileProcessor.Core.Interfaces; // settings service
using FileProcessor.Core.Abstractions; // fs

namespace FileProcessor.Infrastructure.Logging;

// Non-static implementation of IOperationContext.
// Encapsulates operation lifecycle (start/end) and logging setup.
public sealed class OperationContextService : IOperationContext
{
    private readonly IWorkspaceRuntime _runtime;
    private readonly ILogWriteTarget _target;
    private readonly ISettingsService _settings;
    private readonly IFileSystem _fs;
    private readonly TimeProvider _time;
    private Guid _currentOperationGuid = Guid.Empty;
    private string? _currentOperationType;

    public OperationContextService(IWorkspaceRuntime runtime, ILogWriteTarget target, ISettingsService settings, TimeProvider time, IFileSystem fs)
    {
        _runtime = runtime;
        _target = target;
        _settings = settings;
        _time = time;
        _fs = fs;
    }

    public string OperationId { get; private set; } = string.Empty;
    public string LogFilePath { get; private set; } = string.Empty;
    public IItemLogFactory ItemLogFactory { get; private set; } = default!;
    public IOperationStructuredLogger OperationLogger { get; private set; } = default!;

    // Session initialization
    public void Initialize(string operationId, string logFilePath)
    {
        // Use the global logger configured in App for session-level events
        var sessionLogger = Log.Logger.ForContext("scope", "session");
        OperationLogger = new WorkspaceOperationStructuredLogger(sessionLogger);
        ApplySession(operationId, logFilePath);
    }

    private void ApplySession(string sessionId, string logFilePath)
    {
        OperationId = sessionId; // session identifier
        LogFilePath = logFilePath; // session log file
        var options = new ItemLogOptions();
        // During session (no active operation), use empty operation id/type
        ItemLogFactory = new ItemLogFactory(options, OperationLogger, () => Guid.Empty, () => null);
    }

    public async Task StartNewOperationAsync(string? operationType = null)
    {
        var tsSlug = _time.GetUtcNow().ToString("yyyyMMdd_HHmmss");
        var opIdSlug = string.IsNullOrEmpty(operationType) ? tsSlug : $"{operationType}_{tsSlug}";
        _currentOperationGuid = Guid.NewGuid();
        _currentOperationType = operationType ?? "operation";

        var workspacePath = _settings.WorkspaceDirectory;
        if (string.IsNullOrWhiteSpace(workspacePath))
            throw new InvalidOperationException("No workspace configured. Please configure a workspace before starting a new operation.");

        var logsDir = Path.Combine(workspacePath, "logs");
        _fs.CreateDirectory(logsDir);
        var opLogPath = Path.Combine(logsDir, $"operation-{opIdSlug}.jsonl");

        // Global pipeline (includes DB sink); add operation context
        var globalOpLogger = Log.Logger
            .ForContext("scope", "operation")
            .ForContext("operation", _currentOperationGuid)
            .ForContext("operation_type", _currentOperationType);

        // File-only logger for this operation
        var fileOnlyLogger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Async(a => a.File(
                path: opLogPath,
                shared: false,
                formatter: new Serilog.Formatting.Compact.CompactJsonFormatter()))
            .CreateLogger()
            .ForContext("scope", "operation")
            .ForContext("operation", _currentOperationGuid)
            .ForContext("operation_type", _currentOperationType);

        // Composite operation logger that writes to both
        OperationLogger = new CompositeOperationStructuredLogger(
            new WorkspaceOperationStructuredLogger(globalOpLogger),
            new WorkspaceOperationStructuredLogger(fileOnlyLogger));

        // Update factory so item logs carry this operation id/type
        var options = new ItemLogOptions();
        ItemLogFactory = new ItemLogFactory(options, OperationLogger, () => _currentOperationGuid, () => _currentOperationType);

        // Start DB operation row
        try { await _runtime.StartOperationAsync(_currentOperationType, name: opIdSlug); } catch { }
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
        finally
        {
            // Reset to session logger after operation ends
            var sessionLogger = Log.Logger.ForContext("scope", "session");
            OperationLogger = new WorkspaceOperationStructuredLogger(sessionLogger);
            var options = new ItemLogOptions();
            ItemLogFactory = new ItemLogFactory(options, OperationLogger, () => Guid.Empty, () => null);
            _currentOperationGuid = Guid.Empty;
            _currentOperationType = null;
        }
    }

    private sealed class CompositeOperationStructuredLogger : IOperationStructuredLogger
    {
        private readonly IOperationStructuredLogger _a;
        private readonly IOperationStructuredLogger _b;
        public CompositeOperationStructuredLogger(IOperationStructuredLogger a, IOperationStructuredLogger b)
        {
            _a = a; _b = b;
        }
        public void Log(Guid operationId, string? operationType, string itemId, LogSeverity level, string category, string subcategory, string message, object? data = null)
        {
            _a.Log(operationId, operationType, itemId, level, category, subcategory, message, data);
            _b.Log(operationId, operationType, itemId, level, category, subcategory, message, data);
        }
    }
}
