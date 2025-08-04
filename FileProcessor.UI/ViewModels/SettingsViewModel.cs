
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FileProcessor.Core;
using FileProcessor.Core.Interfaces;
using FileProcessor.UI.Services;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace FileProcessor.UI.ViewModels
{
    public partial class SettingsViewModel : ViewModelBase
    {
        private readonly ThemeService _themeService;
        private readonly SettingsService _settingsService;
        private bool _isDarkMode;
        private bool _isUpdatingTheme = false;

        [ObservableProperty]
        private string _workspaceDirectory = "No workspace selected";

        [ObservableProperty]
        private string _inputDirectory = "";

        [ObservableProperty]
        private string _processedDirectory = "";

        [ObservableProperty]
        private string _statusMessage = "Ready";

        [ObservableProperty]
        private bool _hasValidWorkspace = false;

        [ObservableProperty]
        private string _newWorkspacePath = "";

        [ObservableProperty]
        private string _newWorkspaceName = "";

        [ObservableProperty]
        private WorkspaceInfo? _selectedWorkspace;

        [ObservableProperty]
        private int _coreSpareCount = 1;

        [ObservableProperty]
        private int _maxCores = Environment.ProcessorCount;

        [ObservableProperty]
        private int _availableCores = Environment.ProcessorCount - 1;

        public ObservableCollection<WorkspaceInfo> Workspaces { get; } = new();

        public bool IsDarkMode
        {
            get => _isDarkMode;
            set
            {
                if (!_isUpdatingTheme && SetProperty(ref _isDarkMode, value))
                {
                    _isUpdatingTheme = true;
                    _themeService.ChangeTheme(value);
                    _isUpdatingTheme = false;
                }
            }
        }

        public SettingsViewModel()
        {
            _themeService = new ThemeService();
            _settingsService = SettingsService.Instance;
            
            // Get current theme from ThemeService
            _isDarkMode = _themeService.IsCurrentThemeDark();
            
            // Initialize core settings
            CoreSpareCount = _settingsService.CoreSpareCount;
            UpdateAvailableCores();
            
            // Subscribe to workspace changes
            _settingsService.WorkspaceChanged += OnWorkspaceChanged;
            
            // Load workspace settings
            LoadWorkspaceSettings();
            
            // Initialize core settings
            CoreSpareCount = _settingsService.CoreSpareCount;
            UpdateAvailableCores();
        }

        partial void OnCoreSpareCountChanged(int value)
        {
            _settingsService.CoreSpareCount = value;
            UpdateAvailableCores();
        }

        private void UpdateAvailableCores()
        {
            AvailableCores = MaxCores - CoreSpareCount;
        }

        [RelayCommand]
        private async Task AddWorkspaceAsync()
        {
            if (string.IsNullOrWhiteSpace(NewWorkspacePath))
            {
                StatusMessage = "Please enter a workspace path";
                return;
            }

            try
            {
                StatusMessage = "Adding workspace...";
                
                var workspaceName = string.IsNullOrWhiteSpace(NewWorkspaceName) 
                    ? Path.GetFileName(NewWorkspacePath) ?? "Unnamed Workspace"
                    : NewWorkspaceName;
                
                var success = await _settingsService.AddWorkspaceAsync(NewWorkspacePath, workspaceName);
                
                if (success)
                {
                    LoadWorkspaceSettings();
                    NewWorkspacePath = "";
                    NewWorkspaceName = "";
                    StatusMessage = $"Workspace '{workspaceName}' added successfully";
                }
                else
                {
                    StatusMessage = "Failed to add workspace";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error adding workspace: {ex.Message}";
            }
        }

        [RelayCommand]
        private async Task SetActiveWorkspaceAsync(WorkspaceInfo? workspace)
        {
            if (workspace == null) return;

            try
            {
                StatusMessage = "Setting active workspace...";
                
                var success = await _settingsService.SetActiveWorkspaceAsync(workspace.Path);
                
                if (success)
                {
                    LoadWorkspaceSettings();
                    StatusMessage = $"Active workspace set to: {workspace.Name}";
                }
                else
                {
                    StatusMessage = "Failed to set active workspace";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error setting workspace: {ex.Message}";
            }
        }

        [RelayCommand]
        private async Task RemoveWorkspaceAsync(WorkspaceInfo? workspace)
        {
            if (workspace == null) return;

            try
            {
                StatusMessage = "Removing workspace...";
                
                var success = await _settingsService.RemoveWorkspaceAsync(workspace.Path);
                
                if (success)
                {
                    LoadWorkspaceSettings();
                    StatusMessage = $"Workspace '{workspace.Name}' removed";
                }
                else
                {
                    StatusMessage = "Failed to remove workspace";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error removing workspace: {ex.Message}";
            }
        }

        [RelayCommand]
        private void OpenWorkspaceInExplorer()
        {
            try
            {
                if (!string.IsNullOrEmpty(_settingsService.WorkspaceDirectory) && 
                    Directory.Exists(_settingsService.WorkspaceDirectory))
                {
                    // This is a simple approach - in production you'd want cross-platform file manager opening
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = _settingsService.WorkspaceDirectory,
                        UseShellExecute = true
                    });
                    StatusMessage = "Opened workspace in file explorer";
                }
                else
                {
                    StatusMessage = "No valid workspace directory to open";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error opening workspace: {ex.Message}";
            }
        }

        private void OnWorkspaceChanged(object? sender, string? newWorkspace)
        {
            LoadWorkspaceSettings();
        }

        private void LoadWorkspaceSettings()
        {
            // Update workspace list
            Workspaces.Clear();
            foreach (var workspace in _settingsService.Workspaces)
            {
                Workspaces.Add(workspace);
            }

            // Update current workspace display
            if (!string.IsNullOrEmpty(_settingsService.WorkspaceDirectory))
            {
                WorkspaceDirectory = _settingsService.WorkspaceDirectory;
                InputDirectory = _settingsService.InputDirectory ?? "";
                ProcessedDirectory = _settingsService.ProcessedDirectory ?? "";
                HasValidWorkspace = Directory.Exists(_settingsService.WorkspaceDirectory);
                
                SelectedWorkspace = Workspaces.FirstOrDefault(w => 
                    w.Path.Equals(_settingsService.WorkspaceDirectory, StringComparison.OrdinalIgnoreCase));
                
                if (HasValidWorkspace)
                {
                    StatusMessage = "Workspace loaded successfully";
                }
                else
                {
                    StatusMessage = "Workspace directory not found - please select a new one";
                }
            }
            else
            {
                WorkspaceDirectory = "No workspace selected";
                InputDirectory = "";
                ProcessedDirectory = "";
                HasValidWorkspace = false;
                SelectedWorkspace = null;
                StatusMessage = "No workspace configured";
            }
        }
    }
}
