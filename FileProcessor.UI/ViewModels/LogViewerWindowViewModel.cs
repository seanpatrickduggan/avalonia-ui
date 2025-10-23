using FileProcessor.Core.Workspace;
using FileProcessor.Core.Logging;
using LogViewer.UI.ViewModels;

namespace FileProcessor.UI.ViewModels;

// Adapter: reuse the canonical LogViewer.UI viewmodel so FileProcessor.UI remains thin.
public partial class LogViewerWindowViewModel : LogViewer.UI.ViewModels.LogViewerWindowViewModel
{
    // Keep constructor shape compatible with DI used in FileProcessor.UI
    public LogViewerWindowViewModel(IWorkspaceRuntime runtime, IOperationContext opContext, ILogReaderFactory readerFactory)
        : base(runtime, opContext, readerFactory)
    {
        // readerFactory parameter is ignored for now; DB-backed features will be added to canonical VM later
    }
}

// Thin wrapper for LogGroupViewModel to preserve the type expected by FileProcessor.UI views.
public partial class LogGroupViewModel : LogViewer.UI.ViewModels.LogGroupViewModel
{
    public LogGroupViewModel(string category, string subcategory, int count, FileProcessor.Core.Logging.LogSeverity highest, System.Collections.ObjectModel.ObservableCollection<FileProcessor.Core.Logging.ItemLogEntry> entries)
        : base(category, subcategory, count, highest, entries)
    {
    }
}
