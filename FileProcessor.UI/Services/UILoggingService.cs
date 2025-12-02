using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;

namespace FileProcessor.UI.Services;

/// <summary>
/// UI-specific logging helpers.
/// </summary>
public static class UILoggingService
{
    private static FileProcessor.UI.Services.IWindowFactory WindowFactory => CompositionRoot.Get<IWindowFactory>();

    public static void ShowLogViewer()
    {
        var window = WindowFactory.CreateLogViewerWindow();

        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop && desktop.MainWindow != null)
        {
            window.Show(desktop.MainWindow);
        }
        else
        {
            window.Show();
        }
    }

    public static void ShowLogViewer(string logFilePath)
    {
        var window = WindowFactory.CreateLogViewerWindow();

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
