using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using FileProcessor.UI.Views;
using Microsoft.Extensions.DependencyInjection;

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
