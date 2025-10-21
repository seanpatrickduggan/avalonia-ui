using System;
using Microsoft.Extensions.DependencyInjection;
using FileProcessor.Core.Interfaces;
using FileProcessor.Core.Logging;
using FileProcessor.Core.Workspace;
using FileProcessor.Infrastructure.Logging;
using FileProcessor.Infrastructure.Workspace;
using FileProcessor.Core;
using FileProcessor.Core.App;
using FileProcessor.Infrastructure.App;
using LogViewer.UI.ViewModels;
using FileProcessor.Core.Abstractions;
using FileProcessor.Infrastructure.Abstractions;

namespace FileProcessor.UI.Services;

public static class CompositionRoot
{
    private static IServiceProvider? _provider;

    public static IServiceProvider Build()
    {
        if (_provider != null) return _provider;
        var services = new ServiceCollection();

        // Core/Infra services
        services.AddSingleton<ISettingsService>(sp => FileProcessor.Core.SettingsService.Instance);

        // Cross-cutting providers
        services.AddSingleton<TimeProvider>(TimeProvider.System);
        services.AddSingleton<IFileSystem, SystemFileSystem>();

        services.AddSingleton<IWorkspaceDb, SqliteWorkspaceDb>();

        // Instance runtime (owns DB + writer); expose as both runtime and appender
        services.AddSingleton<WorkspaceRuntime>();
        services.AddSingleton<IWorkspaceRuntime>(sp => sp.GetRequiredService<WorkspaceRuntime>());
        services.AddSingleton<ILogAppender>(sp => sp.GetRequiredService<WorkspaceRuntime>());

        // Host-level orchestrator (UI-agnostic)
        services.AddSingleton<IApplicationHost, ApplicationHost>();

        services.AddSingleton<IOperationContext, OperationContextService>();
        services.AddSingleton<ILogReader, SqliteLogReader>();
        services.AddSingleton<ILogReaderFactory>(sp => new LogReaderFactory(sp));

        // Logging target plumbing uses instance-backed runtime + appender
        services.AddSingleton<ILogWriteTarget, WorkspaceLogWriteTarget>();

        // UI window factory
        services.AddSingleton<IWindowFactory, WindowFactory>();

        // ViewModels
        services.AddSingleton<FileProcessor.UI.ViewModels.FileProcessorViewModel>();
        services.AddSingleton<FileProcessor.UI.ViewModels.FileConverterViewModel>();
        services.AddSingleton<FileProcessor.UI.ViewModels.SettingsViewModel>();
        services.AddSingleton<FileProcessor.UI.ViewModels.FileGeneratorViewModel>();
        // Use canonical LogViewer from LogViewer.UI instead of duplicating types here
        services.AddTransient<LogViewer.UI.ViewModels.LogViewerWindowViewModel>();
        services.AddSingleton<FileProcessor.UI.ViewModels.MainWindowViewModel>();

        _provider = services.BuildServiceProvider(validateScopes: false);
        return _provider;
    }

    public static T Get<T>() where T : notnull
        => (_provider ?? Build()).GetRequiredService<T>();
}

internal sealed class LogReaderFactory : ILogReaderFactory
{
    private readonly IServiceProvider _sp;
    public LogReaderFactory(IServiceProvider sp) => _sp = sp;

    public ILogReader ForDatabase() => _sp.GetRequiredService<ILogReader>();
    public ILogReader ForJsonl(string filePath) => new JsonlLogReader(filePath);
}
