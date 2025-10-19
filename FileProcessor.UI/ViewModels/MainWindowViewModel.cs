using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FileProcessor.UI.Services;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using FileProcessor.Core.Workspace;

namespace FileProcessor.UI.ViewModels
{
    public partial class MainWindowViewModel : ObservableObject
    {
        [ObservableProperty]
        private ObservableCollection<NavigationItemViewModel> _navigationItems;

        [ObservableProperty]
        private ViewModelBase _currentPage;

        [ObservableProperty]
        private bool _workspaceInitializing = true;
        [ObservableProperty]
        private bool _workspaceReady = false;
        [ObservableProperty]
        private bool _workspaceError = false;
        [ObservableProperty]
        private string _workspaceStatusMessage = "Initializing workspace...";
        [ObservableProperty]
        private string? _workspaceErrorDetails;

        public MainWindowViewModel()
        {
            NavigationItems = new ObservableCollection<NavigationItemViewModel>
            {
                new NavigationItemViewModel("File Processor", Material.Icons.MaterialIconKind.FolderOpen, CompositionRoot.Get<FileProcessorViewModel>()),
                new NavigationItemViewModel("File Converter", Material.Icons.MaterialIconKind.Cached, CompositionRoot.Get<FileConverterViewModel>()),
                new NavigationItemViewModel("File Generator", Material.Icons.MaterialIconKind.FilePlus, new FileGeneratorViewModel()),
                new NavigationItemViewModel("Settings", Material.Icons.MaterialIconKind.Cog, new SettingsViewModel()),
            };

            CurrentPage = NavigationItems[0].ViewModel;
            
            // Set initial selection
            NavigationItems[0].IsSelected = true;

            // Kick off workspace initialization with health reporting
            _ = InitializeWorkspaceAsync();
        }

        private async Task InitializeWorkspaceAsync()
        {
            var runtime = CompositionRoot.Get<IWorkspaceRuntime>();
            WorkspaceInitializing = true;
            WorkspaceError = false;
            WorkspaceReady = false;
            WorkspaceStatusMessage = "Initializing workspace...";
            WorkspaceErrorDetails = null;
            try
            {
                await runtime.InitializeAsync();
                WorkspaceInitializing = false;
                WorkspaceReady = true;
                WorkspaceStatusMessage = "Workspace ready";
            }
            catch (System.Exception ex)
            {
                WorkspaceInitializing = false;
                WorkspaceReady = false;
                WorkspaceError = true;
                WorkspaceStatusMessage = "Workspace initialization failed";
                WorkspaceErrorDetails = ex.Message;
            }
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

        [RelayCommand]
        private async Task RetryWorkspaceInitAsync()
        {
            await InitializeWorkspaceAsync();
        }

        [RelayCommand]
        private void GoToSettings()
        {
            var settingsItem = NavigationItems.FirstOrDefault(i => i.ViewModel is SettingsViewModel);
            if (settingsItem != null)
            {
                Navigate(settingsItem.ViewModel);
            }
        }
    }
}
