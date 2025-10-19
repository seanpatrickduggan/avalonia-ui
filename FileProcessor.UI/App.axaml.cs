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
using FileProcessor.Core.Workspace; // use runtime
using Microsoft.Extensions.DependencyInjection;
using FileProcessor.UI.Services;

namespace FileProcessor.UI;

public partial class App : Application
{
    public static string OperationId { get; } = $"session_{DateTime.UtcNow:yyyyMMdd_HHmmss}"; // new per-session id
    private string? _logFilePath; // store path
    private System.IServiceProvider? _sp;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
        // Theme
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
        var logPath = Path.Combine(logsDir, $"operation-{OperationId}.jsonl");
        _logFilePath = logPath; // save
        var target = _sp!.GetRequiredService<ILogWriteTarget>();
        Log.Logger = new LoggerConfiguration()
            .Enrich.WithProperty("app", "FileProcessor")
            .Enrich.WithProperty("operation", OperationId)
            .MinimumLevel.Debug()
            .WriteTo.Async(a => a.File(
                path: logPath,
                shared: false,
                formatter: new Serilog.Formatting.Compact.CompactJsonFormatter()))
            .WriteTo.Sink(new WorkspaceSqliteSink(target))
            .CreateLogger();
    }

    public override void OnFrameworkInitializationCompleted()
    {
        // Build DI container
        _sp = CompositionRoot.Build();

        // Logging + DB after XAML is loaded
        ConfigureLogging();
        var op = _sp.GetRequiredService<FileProcessor.Core.Logging.IOperationContext>();
        op.Initialize(OperationId, _logFilePath!);
        var runtime = _sp.GetRequiredService<FileProcessor.Core.Workspace.IWorkspaceRuntime>();
        // runtime init will be awaited when main window opens

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainWindowViewModel()
            };
            desktop.MainWindow.Opened += OnMainWindowOpened;
            desktop.ShutdownRequested += OnShutdownRequested;
        }

        base.OnFrameworkInitializationCompleted();
    }
    
    private async void OnMainWindowOpened(object? sender, EventArgs e)
    {
        var runtime = _sp!.GetRequiredService<IWorkspaceRuntime>();
        try
        {
            await runtime.InitializeAsync();
            var workspacePath = SettingsService.Instance.WorkspaceDirectory!;
            var dbPath = Path.Combine(workspacePath, "workspace.db");
            if (File.Exists(dbPath))
                Log.Information("Workspace initialized. DB at {DbPath}", dbPath);
            else
                Log.Warning("Workspace initialized but DB file not found. Expected {DbPath}", dbPath);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Workspace initialization failed");
        }
    }
    
    private async void OnShutdownRequested(object? sender, ShutdownRequestedEventArgs e)
    {
        var runtime = CompositionRoot.Get<IWorkspaceRuntime>();
        // Save settings before shutdown
        try
        {
            await SettingsService.Instance.SaveSettingsAsync();
        }
        catch { }

        // End active operation and materialize logs
        try { await CompositionRoot.Get<IOperationContext>().EndCurrentOperationAsync("succeeded"); } catch { }
        try { await runtime.MaterializeSessionLogsAsync(runtime.SessionId); } catch { }

        // Flush Serilog then shutdown DB
        try { Serilog.Log.CloseAndFlush(); } catch { }
        try { await runtime.ShutdownAsync(); } catch { }
    }
}
