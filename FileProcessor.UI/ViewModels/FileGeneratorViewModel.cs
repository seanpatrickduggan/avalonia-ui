using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FileProcessor.Core;
using System;
using System.IO;
using System.Threading.Tasks;

namespace FileProcessor.UI.ViewModels
{
    public partial class FileGeneratorViewModel : ViewModelBase
    {
        private readonly FileGenerationService _generationService;
        private readonly string _sampleFilesDirectory;

        [ObservableProperty]
        private int _numberOfFiles = 10;

        [ObservableProperty]
        private string _generationResult = "Ready to generate files.";

        [ObservableProperty]
        private bool _isGenerating = false;

        public FileGeneratorViewModel()
        {
            _generationService = new FileGenerationService();
            _sampleFilesDirectory = Path.Combine(Directory.GetCurrentDirectory(), "SampleFiles");
        }

        [RelayCommand]
        private async Task GenerateFiles()
        {
            IsGenerating = true;
            GenerationResult = "Generating files...";

            try
            {
                await _generationService.GenerateFilesAsync(_sampleFilesDirectory, NumberOfFiles);
                GenerationResult = $"Successfully generated {NumberOfFiles} files in {_sampleFilesDirectory}";
            }
            catch (Exception ex)
            {
                GenerationResult = $"Error generating files: {ex.Message}";
            }
            finally
            {
                IsGenerating = false;
            }
        }
    }
}
