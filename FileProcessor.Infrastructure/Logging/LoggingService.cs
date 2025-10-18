using System;
using System.Diagnostics;
using System.IO;
using FileProcessor.Core.Logging;
using Serilog;
using FileProcessor.Infrastructure.Workspace; // added
using System.Threading.Tasks;
using Serilog.Core;

namespace FileProcessor.Infrastructure.Logging;

public static class LoggingService
{
    public static IItemLogFactory ItemLogFactory { get; private set; } = default!;
    public static IRunStructuredLogger RunLogger { get; private set; } = default!;
    public static string RunId { get; private set; } = string.Empty;
    public static string LogFilePath { get; private set; } = string.Empty;

    public static event Action? LogFileChanged; // notify viewers to refresh path

    private static ILogger? _rootLogger;

    public static void Initialize(ILogger rootLogger, string runId, string logFilePath)
    {
        _rootLogger = rootLogger;
        RunLogger = new WorkspaceRunStructuredLogger(rootLogger); // changed
        ApplyRun(runId, logFilePath);
    }

    private static void ApplyRun(string runId, string logFilePath)
    {
        RunId = runId;
        LogFilePath = logFilePath;
        var options = new ItemLogOptions();
        
        ItemLogFactory = new ItemLogFactory(options, RunLogger, () => Guid.Empty, () => null);
        LogFileChanged?.Invoke();
    }

    public static async void StartNewRun(string? runType = null)
    {
        // Close existing logger sinks if we own them
        try { Log.CloseAndFlush(); } catch { /* ignore */ }
        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
        var runId = string.IsNullOrEmpty(runType) 
            ? $"{timestamp}" 
            : $"{runType}_{timestamp}";
        
        // Get workspace path from settings
        var workspacePath = FileProcessor.Core.SettingsService.Instance.WorkspaceDirectory;
        if (string.IsNullOrWhiteSpace(workspacePath))
        {
            throw new InvalidOperationException("No workspace configured. Please configure a workspace before starting a new run.");
        }
        
        var logsDir = Path.Combine(workspacePath, "logs");
        Directory.CreateDirectory(logsDir);
        var newPath = Path.Combine(logsDir, $"run-{runId}.jsonl");
        
        // Recreate root logger
        _rootLogger = new LoggerConfiguration()
            .Enrich.WithProperty("app", "FileProcessor")
            .Enrich.WithProperty("run", runId) // Use the runId directly
            .MinimumLevel.Debug()
            .WriteTo.Async(a => a.File(
                path: newPath,
                shared: false,
                formatter: new Serilog.Formatting.Compact.CompactJsonFormatter()))
            .WriteTo.Sink(new WorkspaceSqliteSink())
            .CreateLogger();
        RunLogger = new WorkspaceRunStructuredLogger(_rootLogger); // changed
        ApplyRun(runId, newPath);

        // Also start a DB run in the workspace (fire-and-forget for now)
        try { await WorkspaceDbService.StartRunAsync(runType ?? "run", name: runId); } catch { }
    }

    public static async Task EndCurrentRunAsync(string status = "succeeded")
    {
        try
        {
            var rid = WorkspaceDbService.CurrentRunId;
            await WorkspaceDbService.EndRunAsync(status: status);
            // Materialize this run's logs to JSONL alongside Serilog file for parity
            if (rid != 0)
            {
                try { await WorkspaceDbService.MaterializeRunLogsAsync(rid); } catch { }
            }
        }
        catch { }
    }
}
