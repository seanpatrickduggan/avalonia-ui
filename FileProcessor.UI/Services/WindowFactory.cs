using Avalonia.Controls;
using Microsoft.Extensions.DependencyInjection;
using FileProcessor.UI.Views;

namespace FileProcessor.UI.Services;

public sealed class WindowFactory : IWindowFactory
{
    private readonly System.IServiceProvider _sp;
    public WindowFactory(System.IServiceProvider sp) => _sp = sp;

    public Window CreateLogViewerWindow()
    {
        var w = new LogViewerWindow();
        w.DataContext = _sp.GetRequiredService<FileProcessor.UI.ViewModels.LogViewerWindowViewModel>();
        return w;
    }
}
