
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Material.Icons;

namespace FileProcessor.UI.ViewModels
{
    public partial class NavigationItemViewModel : ViewModelBase
    {
        [ObservableProperty]
        private string _name;

        [ObservableProperty]
        private MaterialIconKind _icon;

        [ObservableProperty]
        private ViewModelBase _viewModel;

        [ObservableProperty]
        private bool _isSelected;

        public NavigationItemViewModel(string name, MaterialIconKind icon, ViewModelBase viewModel)
        {
            _name = name;
            _icon = icon;
            _viewModel = viewModel;
            _isSelected = false;
        }
    }
}
