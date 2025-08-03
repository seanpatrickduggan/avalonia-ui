using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace FileProcessor.UI.Views
{
    public partial class FileGeneratorView : UserControl
    {
        public FileGeneratorView()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
