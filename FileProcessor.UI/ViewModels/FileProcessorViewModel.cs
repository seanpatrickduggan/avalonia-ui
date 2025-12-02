using System.Collections.ObjectModel;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using FileProcessor.Core;
using FileProcessor.Core.Logging;
using FileProcessor.UI.Services;

namespace FileProcessor.UI.ViewModels
{
    public partial class FileProcessorViewModel : ViewModelBase
    {
        private readonly FileProcessingService _processingService;
        private readonly string _sampleFilesDirectory;

        public string AppVersion { get; } = GetVersion();
        public string? BuildCommitShort { get; } = GetCommitShort();

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

        private static string GetVersion()
        {
            var asm = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
            var info = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                       ?? asm.GetName().Version?.ToString()
                       ?? "0.0.0";

            // Common formats:
            // 1.2.3+abcdef
            // 1.2.3-alpha+abcdef
            // 1.2.3 (abcdef)
            // v1.2.3-rc.1+abcdef
            var versionOnly = info;
            var plusIdx = info.IndexOf('+');
            if (plusIdx > 0)
            {
                versionOnly = info.Substring(0, plusIdx);
            }
            else
            {
                var match = Regex.Match(info, @"^(v?\d+\.\d+\.\d+[^\s]*)");
                if (match.Success)
                {
                    versionOnly = match.Groups[1].Value;
                }
            }

            return versionOnly.StartsWith('v') ? versionOnly : $"v{versionOnly}";
        }

        private static string? GetCommitShort()
        {
            var asm = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
            var info = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            if (string.IsNullOrWhiteSpace(info)) return null;

            // Extract commit after '+' or within parentheses
            // Prefer short 7-10 chars
            string? commit = null;
            var plusIdx = info.IndexOf('+');
            if (plusIdx > 0 && plusIdx + 1 < info.Length)
            {
                commit = info.Substring(plusIdx + 1).Trim();
            }
            if (string.IsNullOrEmpty(commit))
            {
                var match = Regex.Match(info, @"\((?<commit>[0-9a-fA-F]{7,40})\)");
                if (match.Success)
                {
                    commit = match.Groups["commit"].Value;
                }
            }
            if (string.IsNullOrEmpty(commit)) return null;

            // Shorten
            var shortLen = System.Math.Min(commit.Length, 7);
            return commit[..shortLen];
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
