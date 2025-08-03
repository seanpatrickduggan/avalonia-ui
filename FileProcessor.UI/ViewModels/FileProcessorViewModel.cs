using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using FileProcessor.Core;
using System.IO;

namespace FileProcessor.UI.ViewModels
{
    public partial class FileProcessorViewModel : ViewModelBase
    {
        private readonly FileProcessingService _processingService;
        private readonly string _sampleFilesDirectory;

        [ObservableProperty]
        private string _processingResult = "Ready to process files.";

        public FileProcessorViewModel()
        {
            _processingService = new FileProcessingService();
            _sampleFilesDirectory = Path.Combine(Directory.GetCurrentDirectory(), "SampleFiles");
        }

        [RelayCommand]
        private void ProcessFiles()
        {
            ProcessingResult = "Processing...";
            ProcessingResult = _processingService.ProcessFiles(_sampleFilesDirectory);
        }
    }
}
