using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Input;
using FileProcessor.Infrastructure.Logging;
using FileProcessor.UI.Services;
using FileProcessor.Core.Logging;

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
        var op = CompositionRoot.Get<IOperationContext>();
        if (this.Clipboard != null)
        {
            await this.Clipboard.SetTextAsync(op.LogFilePath);
        }
    }

    private void StartNewRun_Click(object? sender, RoutedEventArgs e)
    {
        var op = CompositionRoot.Get<IOperationContext>();
        _ = op.StartNewOperationAsync("manual");
    }
}
