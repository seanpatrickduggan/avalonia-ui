using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using FileProcessor.UI.Views;
using FileProcessor.UI.Services;
using FileProcessor.Core.Logging;

namespace FileProcessor.UI.Services;

/// <summary>
/// UI-specific logging extensions that depend on Avalonia
/// </summary>
public static class UILoggingService
{
    public static void ShowLogViewer()
    {
        var op = CompositionRoot.Get<IOperationContext>();
        ShowLogViewer(op.LogFilePath);
    }

    public static void ShowLogViewer(string logFilePath)
    {
        var window = new LogViewerWindow();
        
        // If a specific log file path is provided and it's different from current, 
        // the LogViewerWindowViewModel should handle loading it
        // For now, we'll show the current log viewer and the file path will be the current one

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
