using Avalonia;
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
            // MainWindow created in Program after parsing args, skip here if already set
            d.MainWindow ??= new MainWindow();
        }
        base.OnFrameworkInitializationCompleted();
    }
}
