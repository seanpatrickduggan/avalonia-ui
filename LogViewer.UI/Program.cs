using Avalonia;

namespace LogViewer.UI;

internal class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        // Build DI container for standalone mode and keep it for App to resolve ViewModels
        var sp = LogViewer.UI.Services.CompositionRoot.Build(args);
        LogViewer.UI.Services.CompositionRootProvider.ServiceProvider = sp;

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>()
        .UsePlatformDetect()
        .LogToTrace();
}
