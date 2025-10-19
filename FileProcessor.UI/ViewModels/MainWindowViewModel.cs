using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.IO;

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

        // App assigns this to perform centralized initialization
        public Func<Task>? RetryRequested { get; set; }

        public MainWindowViewModel(
            FileProcessorViewModel fileProcessorVm,
            FileConverterViewModel fileConverterVm,
            FileGeneratorViewModel fileGeneratorVm,
            SettingsViewModel settingsVm)
        {
            NavigationItems = new ObservableCollection<NavigationItemViewModel>
            {
                new NavigationItemViewModel("File Processor", Material.Icons.MaterialIconKind.FolderOpen, fileProcessorVm),
                new NavigationItemViewModel("File Converter", Material.Icons.MaterialIconKind.Cached, fileConverterVm),
                new NavigationItemViewModel("File Generator", Material.Icons.MaterialIconKind.FilePlus, fileGeneratorVm),
                new NavigationItemViewModel("Settings", Material.Icons.MaterialIconKind.Cog, settingsVm),
            };

            CurrentPage = NavigationItems[0].ViewModel;
            
            // Set initial selection
            NavigationItems[0].IsSelected = true;

            // Initial state shown until App reports real status
            ReportWorkspaceInitializing();
        }

        // Called by App when it starts initializing the workspace
        public void ReportWorkspaceInitializing()
        {
            WorkspaceInitializing = true;
            WorkspaceError = false;
            WorkspaceReady = false;
            WorkspaceStatusMessage = "Initializing workspace...";
            WorkspaceErrorDetails = null;
        }

        // Called by App when workspace initialized (dbExists indicates presence of db file)
        public void ReportWorkspaceReady(bool dbExists, string? details)
        {
            WorkspaceInitializing = false;
            WorkspaceReady = dbExists;
            WorkspaceError = !dbExists;
            WorkspaceStatusMessage = dbExists ? "Workspace ready" : "Workspace DB missing";
            WorkspaceErrorDetails = details;
        }

        // Called by App when initialization failed
        public void ReportWorkspaceFailed(string error)
        {
            WorkspaceInitializing = false;
            WorkspaceReady = false;
            WorkspaceError = true;
            WorkspaceStatusMessage = "Workspace initialization failed";
            WorkspaceErrorDetails = error;
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
            if (RetryRequested != null)
            {
                ReportWorkspaceInitializing();
                await RetryRequested();
            }
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
