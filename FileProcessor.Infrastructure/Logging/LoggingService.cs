using System;
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
        Guid runGuid = Guid.Empty;
        var parts = runId.Split('_');
        if (parts.Length > 0)
            Guid.TryParse(parts[^1], out runGuid);
        ItemLogFactory = new ItemLogFactory(options, RunLogger, () => runGuid, () => null);
        LogFileChanged?.Invoke();
    }

    public static void StartNewRun(string? runType = null)
    {
        // Close existing logger sinks if we own them
        try { Log.CloseAndFlush(); } catch { /* ignore */ }
        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
        var runId = string.IsNullOrEmpty(runType) 
            ? $"{timestamp}_{Guid.NewGuid():N}" 
            : $"{runType}_{timestamp}";
        var logsDir = Path.Combine(AppContext.BaseDirectory, "logs");
        Directory.CreateDirectory(logsDir);
        var newPath = Path.Combine(logsDir, $"run-{runId}.jsonl");
        // Recreate root logger
        _rootLogger = new LoggerConfiguration()
            .Enrich.WithProperty("app", "FileProcessor")
            .Enrich.WithProperty("run", runId)
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
