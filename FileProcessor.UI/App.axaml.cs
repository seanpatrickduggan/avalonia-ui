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
using FileProcessor.Core.Workspace;
using Microsoft.Extensions.DependencyInjection;
using FileProcessor.UI.Services;
using Serilog.Debugging;
using System.Diagnostics;
using FileProcessor.Core.App;

namespace FileProcessor.UI;

public partial class App : Application
{
    public static string OperationId { get; } = $"session_{DateTime.UtcNow:yyyyMMdd_HHmmss}";
    private string? _logFilePath; // store path
    private System.IServiceProvider? _sp;
    private IClassicDesktopStyleApplicationLifetime? _desktop;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
        // Theme
        RequestedThemeVariant = ThemeVariant.Dark;
    }

    private void ConfigureLogging()
    {
#if DEBUG
        SelfLog.Enable(msg => Debug.WriteLine(msg));
#endif
        // Get workspace path from settings
        var workspacePath = SettingsService.Instance.WorkspaceDirectory;
        if (string.IsNullOrWhiteSpace(workspacePath))
        {
            // Don't fail startup when running headless tests or when the user hasn't configured a workspace yet.
            // Use a minimal console/debug logger and leave _logFilePath null/empty so Initialize can still be called.
            _logFilePath = string.Empty;
            Log.Logger = new LoggerConfiguration()
                .Enrich.WithProperty("app", "FileProcessor")
                .Enrich.WithProperty("operation", OperationId)
                .MinimumLevel.Debug()
                .CreateLogger();
            return;
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
        op.Initialize(OperationId, _logFilePath ?? string.Empty);

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            _desktop = desktop;
            desktop.MainWindow = new MainWindow
            {
                DataContext = _sp.GetRequiredService<MainWindowViewModel>()
            };
            desktop.MainWindow.Opened += OnMainWindowOpened;
            desktop.ShutdownRequested += OnShutdownRequested;

            // Wire RetryRequested from VM to centralized init
            if (desktop.MainWindow.DataContext is MainWindowViewModel vm)
            {
                vm.RetryRequested = async () => await InitializeWorkspaceAsync();
            }
        }

        base.OnFrameworkInitializationCompleted();
    }

    private async System.Threading.Tasks.Task InitializeWorkspaceAsync()
    {
        var host = _sp!.GetRequiredService<IApplicationHost>();
        try
        {
            if (_desktop?.MainWindow?.DataContext is MainWindowViewModel vm)
                vm.ReportWorkspaceInitializing();

            await host.InitializeAsync();
            var workspacePath = SettingsService.Instance.WorkspaceDirectory!;
            var dbPath = Path.Combine(workspacePath, "workspace.db");
            var exists = File.Exists(dbPath);
            if (exists)
                Log.Information("Workspace initialized. DB at {DbPath}", dbPath);
            else
                Log.Warning("Workspace initialized but DB file not found. Expected {DbPath}", dbPath);

            if (_desktop?.MainWindow?.DataContext is MainWindowViewModel vm2)
                vm2.ReportWorkspaceReady(exists, exists ? null : $"Expected at: {dbPath}");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Workspace initialization failed");
            if (_desktop?.MainWindow?.DataContext is MainWindowViewModel vm3)
                vm3.ReportWorkspaceFailed(ex.Message + (ex.InnerException != null ? " | " + ex.InnerException.Message : string.Empty));
        }
    }

    private async void OnMainWindowOpened(object? sender, EventArgs e)
    {
        await InitializeWorkspaceAsync();
    }

    private async void OnShutdownRequested(object? sender, ShutdownRequestedEventArgs e)
    {
        var host = _sp!.GetRequiredService<IApplicationHost>();
        var runtime = _sp!.GetRequiredService<IWorkspaceRuntime>();
        // Save settings before shutdown
        try
        {
            await SettingsService.Instance.SaveSettingsAsync();
        }
        catch { }

        // End active operation and materialize logs
        try { await _sp!.GetRequiredService<IOperationContext>().EndCurrentOperationAsync("succeeded"); } catch { }
        try { await runtime.MaterializeSessionLogsAsync(runtime.SessionId); } catch { }

        // Coordinate shutdown via host (flush + db shutdown)
        try { await host.ShutdownAsync(); } catch { }
        try { Serilog.Log.CloseAndFlush(); } catch { }
    }
}
