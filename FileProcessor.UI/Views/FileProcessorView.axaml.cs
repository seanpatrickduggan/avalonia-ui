
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace FileProcessor.UI.Views
{
    public partial class FileProcessorView : UserControl
    {
        public FileProcessorView()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
