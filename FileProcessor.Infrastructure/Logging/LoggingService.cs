using System;
using System.Diagnostics;
using System.IO;
using FileProcessor.Core.Logging;
using Serilog;

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
        RunLogger = new SerilogRunStructuredLogger(rootLogger);
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

    public static void StartNewRun(string? runType = null)
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
            .CreateLogger();
        RunLogger = new SerilogRunStructuredLogger(_rootLogger);
        ApplyRun(runId, newPath);
    }
}
