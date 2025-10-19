using Avalonia.Controls;
using Avalonia.Interactivity;

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
        if (this.Clipboard != null && DataContext is FileProcessor.UI.ViewModels.LogViewerWindowViewModel vm)
        {
            await this.Clipboard.SetTextAsync(vm.LogFilePath);
        }
    }
}
