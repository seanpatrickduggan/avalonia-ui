using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using FileProcessor.Core;

using System;
using System.Threading.Tasks;

namespace FileProcessor.UI.ViewModels
{
    public partial class FileGeneratorViewModel : ViewModelBase
    {
        private readonly FileGenerationService _generationService;
        private readonly SettingsService _settingsService;

        [ObservableProperty]
        private int _numberOfFiles = 10;

        [ObservableProperty]
        private string _generationResult = "Ready to generate files.";

        [ObservableProperty]
        private bool _isGenerating = false;

        [ObservableProperty]
        private double _generationProgress = 0;

        [ObservableProperty]
        private string _progressText = "";

        [ObservableProperty]
        private string _targetDirectory = "No workspace configured";

        public FileGeneratorViewModel()
        {
            _generationService = new FileGenerationService();
            _settingsService = SettingsService.Instance;

            // Subscribe to workspace changes
            _settingsService.WorkspaceChanged += OnWorkspaceChanged;

            UpdateTargetDirectory();
        }

        [RelayCommand]
        private async Task GenerateFiles()
        {
            var targetDir = GetTargetDirectory();
            if (string.IsNullOrEmpty(targetDir))
            {
                GenerationResult = "Please configure a workspace in Settings first";
                return;
            }

            IsGenerating = true;
            GenerationProgress = 0;
            GenerationResult = "Generating files...";
            ProgressText = "";

            try
            {
                var progress = new Progress<(int completed, int total)>(report =>
                {
                    GenerationProgress = (double)report.completed / report.total * 100;
                    ProgressText = $"Generated {report.completed:N0} of {report.total:N0} files";
                });

                await _generationService.GenerateFilesAsync(targetDir, NumberOfFiles, progress);
                GenerationResult = $"Successfully generated {NumberOfFiles:N0} files in {targetDir}";
                ProgressText = "Generation complete!";
            }
            catch (Exception ex)
            {
                GenerationResult = $"Error generating files: {ex.Message}";
                ProgressText = "Generation failed.";
            }
            finally
            {
                IsGenerating = false;
            }
        }

        [RelayCommand]
        private async Task Generate100kFiles()
        {
            NumberOfFiles = 100000;
            await GenerateFiles();
        }

        private string? GetTargetDirectory()
        {
            return _settingsService.InputDirectory;
        }

        private void UpdateTargetDirectory()
        {
            var targetDir = GetTargetDirectory();
            TargetDirectory = string.IsNullOrEmpty(targetDir) ? "No workspace configured" : targetDir;
        }

        private void OnWorkspaceChanged(object? sender, string? newWorkspace)
        {
            UpdateTargetDirectory();
        }
    }
}
