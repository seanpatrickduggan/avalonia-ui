using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Input;
using FileProcessor.UI.Services;

namespace FileProcessor.UI.Views;

public partial class LogViewerWindow : Window
{
    public LogViewerWindow()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        Avalonia.Markup.Xaml.AvaloniaXamlLoader.Load(this);
    }

    private async void CopyPath_Click(object? sender, RoutedEventArgs e)
    {
        if (this.Clipboard != null)
        {
            await this.Clipboard.SetTextAsync(LoggingService.LogFilePath);
        }
    }

    private void StartNewRun_Click(object? sender, RoutedEventArgs e)
    {
        LoggingService.StartNewRun();
    }
}
