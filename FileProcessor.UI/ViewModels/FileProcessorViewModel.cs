using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using FileProcessor.Core;
using System.IO;
using FileProcessor.Infrastructure.Logging;
using FileProcessor.UI.Services;
using FileProcessor.Core.Logging;
using System.Collections.ObjectModel;
using FileProcessor.UI.Views;
using Avalonia.Controls;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;

namespace FileProcessor.UI.ViewModels
{
    public partial class FileProcessorViewModel : ViewModelBase
    {
        private readonly FileProcessingService _processingService;
        private readonly string _sampleFilesDirectory;

        [ObservableProperty]
        private string _processingResult = "Ready to process files.";

        [ObservableProperty]
        private ObservableCollection<ItemLogResult> _lastItemLogs = new(); // expose results

        [ObservableProperty]
        private ItemLogResult? _selectedItemLog; // selected summary

        [ObservableProperty]
        private ObservableCollection<ItemLogEntry> _selectedEntries = new(); // entries of selected item

        public FileProcessorViewModel(IOperationContext opContext)
        {
            _processingService = new FileProcessingService(opContext.ItemLogFactory);
            _sampleFilesDirectory = Path.Combine(Directory.GetCurrentDirectory(), "SampleFiles");
        }

        [RelayCommand]
        private void ProcessFiles()
        {
            LastItemLogs.Clear();
            ProcessingResult = "Processing...";
            try
            {
                var (summary, logs) = _processingService.ProcessFilesWithLogs(_sampleFilesDirectory);
                ProcessingResult = summary;
                foreach (var log in logs)
                {
                    LastItemLogs.Add(log);
                }
            }
            finally
            {
                UILoggingService.ShowLogViewer();
            }
        }

        partial void OnSelectedItemLogChanged(ItemLogResult? value)
        {
            SelectedEntries.Clear();
            if (value == null) return;
            foreach (var e in value.Entries)
            {
                SelectedEntries.Add(e);
            }
        }
    }
}
