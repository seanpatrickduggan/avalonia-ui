using System;
using Microsoft.Extensions.DependencyInjection;
using FileProcessor.Core.Interfaces;
using FileProcessor.Core.Logging;
using FileProcessor.Core.Workspace;
using FileProcessor.Infrastructure.Logging;
using FileProcessor.Infrastructure.Workspace;
using FileProcessor.Core;

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
        services.AddSingleton<IWorkspaceDb, SqliteWorkspaceDb>();
        services.AddSingleton<IWorkspaceRuntime, WorkspaceRuntimeAdapter>();
        services.AddSingleton<IOperationContext, OperationContextService>();
        services.AddSingleton<ILogReader, SqliteLogReader>(); // default DB-backed reader
        services.AddSingleton<ILogReaderFactory>(sp => new LogReaderFactory(sp));

        // ViewModels
        services.AddSingleton<FileProcessor.UI.ViewModels.FileProcessorViewModel>();
        services.AddSingleton<FileProcessor.UI.ViewModels.FileConverterViewModel>();

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
