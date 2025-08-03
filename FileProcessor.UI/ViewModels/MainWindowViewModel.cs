
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Linq;

namespace FileProcessor.UI.ViewModels
{
    public partial class MainWindowViewModel : ObservableObject
    {
        [ObservableProperty]
        private ObservableCollection<NavigationItemViewModel> _navigationItems;

        [ObservableProperty]
        private ViewModelBase _currentPage;

        public MainWindowViewModel()
        {
            NavigationItems = new ObservableCollection<NavigationItemViewModel>
            {
                new NavigationItemViewModel("File Processor", Material.Icons.MaterialIconKind.FolderOpen, new FileProcessorViewModel()),
                new NavigationItemViewModel("File Generator", Material.Icons.MaterialIconKind.FilePlus, new FileGeneratorViewModel()),
                new NavigationItemViewModel("Settings", Material.Icons.MaterialIconKind.Cog, new SettingsViewModel()),
            };

            CurrentPage = NavigationItems[0].ViewModel;
            
            // Set initial selection
            NavigationItems[0].IsSelected = true;
        }

        [RelayCommand]
        private void Navigate(ViewModelBase viewModel)
        {
            // Update the current page
            CurrentPage = viewModel;
            
            // Update selection state for all navigation items
            foreach (var item in NavigationItems)
            {
                item.IsSelected = item.ViewModel == viewModel;
            }
        }
    }
}
