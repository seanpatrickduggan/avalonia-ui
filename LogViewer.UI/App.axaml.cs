using Avalonia;
using Microsoft.Extensions.DependencyInjection;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

namespace LogViewer.UI;

public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime d)
        {
            // MainWindow created by XAML; ensure it's set and set DataContext from DI if available
            d.MainWindow ??= new MainWindow();
            if (d.MainWindow is MainWindow mw && LogViewer.UI.Services.CompositionRootProvider.ServiceProvider is IServiceProvider sp)
            {
                try { mw.DataContext = sp.GetRequiredService<MainWindowViewModel>(); } catch { }
            }
        }
        base.OnFrameworkInitializationCompleted();
    }
}
