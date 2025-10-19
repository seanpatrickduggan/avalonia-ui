using System.Threading;
using System.Threading.Tasks;
using FileProcessor.Core.Interfaces;
using FileProcessor.Core.Workspace;
using FileProcessor.Infrastructure.Abstractions;
using FileProcessor.Infrastructure.Diagnostics;
using FileProcessor.Infrastructure.Workspace;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace FileProcessor.Tests.Integration;

public class IntegrationProbeTests
{
    [Fact]
    public async Task OneEventResultsInOneRow()
    {
        using var ws = new TempWorkspace();
        // Build DI similar to app but with test workspace
        var services = new ServiceCollection();
        services.AddSingleton<ISettingsService>(sp => FileProcessor.Core.SettingsService.Instance);
        // Force settings to point to temp workspace
        FileProcessor.Core.SettingsService.Instance.WorkspaceDirectory = ws.Root;

        services.AddSingleton<System.TimeProvider>(System.TimeProvider.System);
        services.AddSingleton<FileProcessor.Core.Abstractions.IFileSystem, SystemFileSystem>();
        services.AddSingleton<IWorkspaceDb, SqliteWorkspaceDb>();
        services.AddSingleton<WorkspaceRuntime>();
        services.AddSingleton<IWorkspaceRuntime>(sp => sp.GetRequiredService<WorkspaceRuntime>());

        var sp = services.BuildServiceProvider();
        var runtime = sp.GetRequiredService<IWorkspaceRuntime>();
        var db = sp.GetRequiredService<IWorkspaceDb>();
        var time = sp.GetRequiredService<System.TimeProvider>();

        var (ok, count, marker) = await IntegrationProbe.VerifyOneEventOneRowAsync(runtime, db, time, CancellationToken.None);

        ok.Should().BeTrue($"probe marker {marker} should appear exactly once");
        count.Should().Be(1);
    }
}
