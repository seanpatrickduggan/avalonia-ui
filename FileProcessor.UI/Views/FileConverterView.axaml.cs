using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace FileProcessor.UI.Views;

public partial class FileConverterView : UserControl
{
    public FileConverterView()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
