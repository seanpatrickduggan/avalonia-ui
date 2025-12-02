using Microsoft.Extensions.DependencyInjection;
using FileProcessor.Core.Workspace;
using FileProcessor.Core.Logging;

namespace LogViewer.UI.Services;

internal static class CompositionRoot
{
    public static IServiceProvider Build(string?[] args)
    {
        var services = new ServiceCollection();

        // Register optional runtime services with lightweight fakes suitable for standalone file-mode.
        services.AddSingleton<IWorkspaceRuntime, FakeWorkspaceRuntime>();
        services.AddSingleton<IOperationContext>(sp =>
        {
            var op = new FakeOperationContext();
            // if args[0] is a path, set as initial LogFilePath
            var p = (args != null && args.Length > 0 && !string.IsNullOrWhiteSpace(args[0])) ? args[0] : string.Empty;
            op.Initialize("standalone", p!);
            return op;
        });

        services.AddSingleton<FileProcessor.Core.Workspace.ILogReaderFactory, ContextAwareLogReaderFactory>();

        // Register ViewModel
        services.AddTransient<MainWindowViewModel>();

        return services.BuildServiceProvider();
    }
}
