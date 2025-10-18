using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using FileProcessor.UI.ViewModels;
using FileProcessor.Core;
using Serilog;
using System;
using FileProcessor.Core.Logging;
using System.IO;
using FileProcessor.Infrastructure.Logging;

namespace FileProcessor.UI;

public partial class App : Application
{
    public static string RunId { get; } = $"session_{DateTime.UtcNow:yyyyMMdd_HHmmss}"; // new per-session id
    private string? _logFilePath; // store path

    public override void Initialize()
    {
        ConfigureLogging();
        LoggingService.Initialize(Log.Logger, RunId, _logFilePath!); // initialize logging helpers with path
        AvaloniaXamlLoader.Load(this); // keep loader

        // Set initial theme to Dark
        RequestedThemeVariant = ThemeVariant.Dark;
    }

    private void ConfigureLogging()
    {
        // Get workspace path from settings
        var workspacePath = SettingsService.Instance.WorkspaceDirectory;
        if (string.IsNullOrWhiteSpace(workspacePath))
        {
            throw new InvalidOperationException("No workspace configured. Please configure a workspace before starting the application.");
        }

        var logsDir = Path.Combine(workspacePath, "logs");
        Directory.CreateDirectory(logsDir);
        var logPath = Path.Combine(logsDir, $"run-{RunId}.jsonl");
        _logFilePath = logPath; // save
        Log.Logger = new LoggerConfiguration()
            .Enrich.WithProperty("app", "FileProcessor")
            .Enrich.WithProperty("run", RunId)
            .MinimumLevel.Debug()
            .WriteTo.Async(a => a.File(
                path: logPath,
                shared: false,
                formatter: new Serilog.Formatting.Compact.CompactJsonFormatter()))
            .CreateLogger();
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainWindowViewModel()
            };
            
            // Handle application shutdown to save settings
            desktop.ShutdownRequested += OnShutdownRequested;
        }

        base.OnFrameworkInitializationCompleted();
    }
    
    private async void OnShutdownRequested(object? sender, ShutdownRequestedEventArgs e)
    {
        // Save settings before shutdown
        try
        {
            await SettingsService.Instance.SaveSettingsAsync();
        }
        catch
        {
            // Ignore save errors during shutdown
        }
    }
}
