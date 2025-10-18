using System;
using FileProcessor.Infrastructure.Logging;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using FileProcessor.UI.Views;

namespace FileProcessor.UI.Services;

/// <summary>
/// UI-specific logging extensions that depend on Avalonia
/// </summary>
public static class UILoggingService
{
    public static void ShowLogViewer()
    {
        ShowLogViewer(LoggingService.LogFilePath);
    }

    public static void ShowLogViewer(string logFilePath)
    {
        var window = new LogViewerWindow();
        
        // If a specific log file path is provided and it's different from current, 
        // the LogViewerWindowViewModel should handle loading it
        if (!string.IsNullOrEmpty(logFilePath) && logFilePath != LoggingService.LogFilePath)
        {
            // The LogViewerWindowViewModel will need to be updated to accept a specific file path
            // For now, we'll show the current log viewer and the file path will be the current one
        }

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
