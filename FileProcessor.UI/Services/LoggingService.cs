using System;
using System.IO;
using FileProcessor.Core.Logging;
using Serilog;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using FileProcessor.UI.Views;

namespace FileProcessor.UI.Services;

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

    public static void StartNewRun()
    {
        // Close existing logger sinks if we own them
        try { Log.CloseAndFlush(); } catch { /* ignore */ }
        var newRunId = $"{DateTime.UtcNow:yyyyMMdd_HHmmss}_{Guid.NewGuid():N}";
        var logsDir = Path.Combine(AppContext.BaseDirectory, "logs");
        Directory.CreateDirectory(logsDir);
        var newPath = Path.Combine(logsDir, $"run-{newRunId}.jsonl");
        // Recreate root logger
        _rootLogger = new LoggerConfiguration()
            .Enrich.WithProperty("app", "FileProcessor")
            .Enrich.WithProperty("run", newRunId)
            .MinimumLevel.Debug()
            .WriteTo.Async(a => a.File(
                path: newPath,
                shared: false,
                formatter: new Serilog.Formatting.Json.JsonFormatter(renderMessage: true)))
            .CreateLogger();
        RunLogger = new SerilogRunStructuredLogger(_rootLogger);
        ApplyRun(newRunId, newPath);
    }

    public static void ShowLogViewer()
    {
        var window = new LogViewerWindow();
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop && desktop.MainWindow != null)
        {
            window.Show(desktop.MainWindow);
        }
        else
        {
            window.Show();
        }
    }
}
