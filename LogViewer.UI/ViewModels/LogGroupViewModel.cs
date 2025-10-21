using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FileProcessor.Core.Logging;

namespace LogViewer.UI.ViewModels
{
    public partial class LogGroupViewModel : ObservableObject
    {
        public string Category { get; }
        public string Subcategory { get; }
        public int Count { get; }
        public LogSeverity HighestSeverity { get; }
        public ObservableCollection<ItemLogEntry> Entries { get; }

        [ObservableProperty]
        private bool _isExpanded;

        public RelayCommand ToggleCommand { get; }

        public string Header => $"{Subcategory} ({Count})";

        public string SeveritySummary { get; }

        public LogGroupViewModel(string category, string subcategory, int count, LogSeverity highest, ObservableCollection<ItemLogEntry> entries)
        {
            Category = category;
            Subcategory = subcategory;
            Count = count;
            HighestSeverity = highest;
            Entries = entries;
            _isExpanded = highest >= LogSeverity.Error;
            ToggleCommand = new RelayCommand(() => IsExpanded = !IsExpanded);
            SeveritySummary = string.Empty;
        }
    }
}
