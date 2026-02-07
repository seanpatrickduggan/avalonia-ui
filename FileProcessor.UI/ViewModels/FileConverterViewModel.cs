using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FileProcessor.Core;
using FileProcessor.Core.Interfaces;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Threading;
using FileProcessor.UI.Services;
using FileProcessor.Core.Logging;

namespace FileProcessor.UI.ViewModels;

public partial class FileConverterViewModel : ViewModelBase
{
    private readonly FileProcessingService _fileProcessingService;
    private readonly SettingsService _settingsService;
    private readonly IOperationContext _opContext;
    private CancellationTokenSource? _checkCancellationTokenSource;
    private CancellationTokenSource? _processingCancellationTokenSource;
    private bool _isBulkSelectionUpdate;
    private List<FileItemViewModel> _allFiles = new();

    [ObservableProperty]
    private bool _isProcessing;

    [ObservableProperty]
    private int _progressValue;

    [ObservableProperty]
    private string _progressText = "Ready";

    [ObservableProperty]
    private bool _selectAll;

    [ObservableProperty]
    private int _selectedFileCount;

    [ObservableProperty]
    private string _outputDirectory = "";

    [ObservableProperty]
    private bool _hasOutputDirectory;

    [ObservableProperty]
    private bool _isLoadingFiles;

    [ObservableProperty]
    private int _loadingProgress;

    [ObservableProperty]
    private string _loadingProgressText = "";

    [ObservableProperty]
    private bool _isCheckingFiles;

    [ObservableProperty]
    private string _checkProgressText = "";

    [ObservableProperty]
    private bool _hasSelectedWorkspaces = false;

    [ObservableProperty]
    private bool _hasAvailableWorkspaces = false;

    [ObservableProperty]
    private string _sortColumn = "";

    [ObservableProperty]
    private bool _sortAscending = true;

    [ObservableProperty]
    private string _fileSearchText = "";

    // Computed property for check button enabled state
    public bool CanCheckFilesEnabled
    {
        get
        {
            var result = HasSelectedWorkspaces && !IsCheckingFiles;
            return result;
        }
    }

    // Manual command property that we can control
    public ICommand CheckFilesCommand { get; private set; } = null!;

    public ObservableCollection<FileItemViewModel> Files { get; } = new();
    public ObservableCollection<WorkspaceSelectionViewModel> AvailableWorkspaces { get; } = new();

    // Track per-item highest severity (thread-safe) for future batch viewer use
    private readonly ConcurrentDictionary<string, LogSeverity> _highestSeverityByFile = new();
    private readonly ConcurrentBag<ItemLogResult> _conversionItemLogResults = new();

    public FileConverterViewModel(IOperationContext opContext)
    {
        _opContext = opContext;
        _fileProcessingService = new FileProcessingService(opContext.ItemLogFactory);
        _settingsService = SettingsService.Instance;

        // Create manual command with explicit CanExecute control
        CheckFilesCommand = new RelayCommand(
            execute: async () => await CheckFilesAsync(),
            canExecute: () =>
            {
                var canExecute = HasSelectedWorkspaces && !IsCheckingFiles;
                return canExecute;
            });

        // Subscribe to workspace changes
        _settingsService.WorkspaceChanged += OnWorkspaceChanged;

        // Set output directory from workspace settings
        UpdateOutputDirectory();

        // Load available workspaces
        LoadAvailableWorkspaces();

        // Don't automatically load files - wait for user to select workspaces and click Check
    }

    private async Task CheckFilesAsync()
    {
        var selectedWorkspaces = AvailableWorkspaces.Where(w => w.IsSelected).ToList();

        if (!selectedWorkspaces.Any())
        {
            ProgressText = "Please select at least one workspace to check";
            return;
        }

        if (string.IsNullOrEmpty(OutputDirectory))
        {
            ProgressText = "Please configure output directory first";
            return;
        }

        // Cancel any existing check operation
        _checkCancellationTokenSource?.Cancel();
        _checkCancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = _checkCancellationTokenSource.Token;

        IsCheckingFiles = true;
        CheckProgressText = "Loading files from selected workspaces...";

        try
        {
            // Clear existing files
            Files.Clear();

            // Load files from all selected workspaces
            var allFiles = new List<FileItemViewModel>();

            foreach (var workspaceSelection in selectedWorkspaces)
            {
                var inputDir = Path.Combine(workspaceSelection.Workspace.Path, "input");

                if (!Directory.Exists(inputDir))
                {
                    CheckProgressText = $"Input directory not found for workspace: {workspaceSelection.Workspace.Name}";
                    continue;
                }

                var workspaceFiles = Directory.GetFiles(inputDir, "*.txt");
                CheckProgressText = $"Loading files from {workspaceSelection.Workspace.Name}...";

                foreach (var filePath in workspaceFiles)
                {
                    var fileInfo = new FileInfo(filePath);
                    var fileItem = new FileItemViewModel
                    {
                        FileName = fileInfo.Name,
                        FilePath = filePath,
                        FileSize = $"{fileInfo.Length:N0} bytes",
                        LastModified = fileInfo.LastWriteTime.ToString("yyyy-MM-dd HH:mm"),
                        IsSelected = false,
                        WorkspaceName = workspaceSelection.Workspace.Name
                    };
                    allFiles.Add(fileItem);
                }
            }

            CheckProgressText = $"Checking {allFiles.Count} files for conversion status...";

            var needsConversion = 0;
            var upToDate = 0;
            var processedCount = 0;

            var selectionResults = new bool[allFiles.Count];
            var statusResults = new string[allFiles.Count];
            var noteResults = new string[allFiles.Count];

            var parallelOptions = new ParallelOptions
            {
                CancellationToken = cancellationToken,
                MaxDegreeOfParallelism = _settingsService.MaxDegreeOfParallelism
            };

            await Task.Run(() =>
            {
                Parallel.For(0, allFiles.Count, parallelOptions, i =>
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var file = allFiles[i];
                    var outputFileName = Path.GetFileNameWithoutExtension(file.FilePath) + "_converted.json";
                    var outputFilePath = Path.Combine(OutputDirectory, outputFileName);

                    if (!File.Exists(outputFilePath))
                    {
                        Interlocked.Increment(ref needsConversion);
                        selectionResults[i] = true;
                        statusResults[i] = "Not converted";
                        noteResults[i] = "No output";
                    }
                    else
                    {
                        var buildMatch = IsOutputBuildMatch(outputFilePath);
                        if (!buildMatch)
                        {
                            Interlocked.Increment(ref needsConversion);
                            selectionResults[i] = true;
                            statusResults[i] = "Build mismatch";
                            noteResults[i] = "Version/hash changed";
                        }
                        else
                        {
                        var inputUtc = File.GetLastWriteTimeUtc(file.FilePath);
                        var outputUtc = File.GetLastWriteTimeUtc(outputFilePath);
                        var delta = inputUtc - outputUtc;

                        if (delta <= TimeSpan.FromSeconds(1))
                        {
                            Interlocked.Increment(ref upToDate);
                            selectionResults[i] = false;
                            statusResults[i] = "Up to Date";
                            noteResults[i] = "Current";
                        }
                        else
                        {
                            Interlocked.Increment(ref needsConversion);
                            selectionResults[i] = true;
                            statusResults[i] = "Output outdated";
                            noteResults[i] = "Output outdated";
                        }
                        }
                    }

                    var currentCount = Interlocked.Increment(ref processedCount);
                    if (currentCount % 25 == 0 || currentCount == allFiles.Count)
                    {
                        _ = Dispatcher.UIThread.InvokeAsync(() =>
                        {
                            CheckProgressText = $"Checking file {currentCount} of {allFiles.Count}...";
                        });
                    }
                });
            }, cancellationToken);

            for (var i = 0; i < allFiles.Count; i++)
            {
                var file = allFiles[i];
                file.IsSelected = selectionResults[i];
                file.UpdateStatus(statusResults[i]);
                file.ConversionNote = noteResults[i];
            }

            // Sort files by status then filename before adding to UI
            var sortedFiles = allFiles
                .OrderBy(f => f.Status)
                .ThenBy(f => f.FileName)
                .ToList();

            foreach (var fileItem in sortedFiles)
            {
                fileItem.PropertyChanged += OnFileSelectionChanged;
            }

            // Set default sort column to status before applying filter
            SortColumn = "status";
            SortAscending = true;

            _allFiles = sortedFiles;
            ApplyFileFilter();

            ProgressText = $"Check complete: {allFiles.Count} total files, {needsConversion} need conversion, {upToDate} up to date, {SelectedFileCount} selected";
            CheckProgressText = "Check complete";
        }
        catch (OperationCanceledException)
        {
            ProgressText = "Check operation was cancelled";
            CheckProgressText = "Check cancelled";
        }
        catch (Exception ex)
        {
            ProgressText = $"Error during check: {ex.Message}";
            CheckProgressText = "Check failed";
        }
        finally
        {
            IsCheckingFiles = false;
            _checkCancellationTokenSource?.Dispose();
            _checkCancellationTokenSource = null;
        }
    }

    private bool CanCheckFiles()
    {
        return HasSelectedWorkspaces && !IsCheckingFiles;
    }

    [RelayCommand]
    private void CancelCheck()
    {
        _checkCancellationTokenSource?.Cancel();
    }

    [RelayCommand]
    private async Task ProcessSelectedFilesAsync()
    {
        var selectedFiles = Files.Where(f => f.IsSelected).ToArray();
        if (!selectedFiles.Any())
        {
            ProgressText = "No files selected";
            return;
        }

        if (string.IsNullOrEmpty(OutputDirectory))
        {
            ProgressText = "Please select an output directory first";
            return;
        }

        // Start a fresh log run for this batch with descriptive name
        await _opContext.StartNewOperationAsync("conversion");

        // Cancel any existing processing operation
        _processingCancellationTokenSource?.Cancel();
        _processingCancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = _processingCancellationTokenSource.Token;

        IsProcessing = true;
        ProgressValue = 0;
        ProgressText = $"Converting {selectedFiles.Length} files...";

        try
        {
            var converted = 0;
            var errors = 0;
            var processedCount = 0;

            var parallelOptions = new ParallelOptions
            {
                CancellationToken = cancellationToken,
                MaxDegreeOfParallelism = _settingsService.MaxDegreeOfParallelism
            };

            await Task.Run(() =>
            {
                Parallel.ForEach(selectedFiles, parallelOptions, file =>
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    // Start per-file scoped logging
                    using var scope = _opContext.ItemLogFactory.Start(file.FileName);
                    var sw = System.Diagnostics.Stopwatch.StartNew();
                    FileInfo? inputInfo = null;
                    string? outputFilePath = null;
                    try
                    {
                        // Update UI status
                        _ = Dispatcher.UIThread.InvokeAsync(() =>
                        {
                            file.UpdateStatus("Processing");
                            ProgressText = $"Converting {file.FileName}...";
                        });

                        inputInfo = new FileInfo(file.FilePath);
                        var outputFileName = Path.GetFileNameWithoutExtension(file.FilePath) + "_converted.json";
                        outputFilePath = Path.Combine(OutputDirectory, outputFileName);

                        scope.Info("Starting conversion", new
                        {
                            input = file.FilePath,
                            output = outputFilePath,
                            sizeBytes = inputInfo.Length,
                            lastWriteUtc = inputInfo.LastWriteTimeUtc
                        }, category: "convert");

                        // Read content for metrics (so we can log before conversion write)
                        string inputContent = File.ReadAllText(file.FilePath);
                        var lineCount = inputContent.Split('\n').Length;
                        var wordCount = inputContent.Split(new[] { ' ', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries).Length;
                        var charCount = inputContent.Length;

                        scope.Debug("Read input file", new
                        {
                            lineCount,
                            wordCount,
                            charCount,
                            preview = inputContent.Length > 120 ? inputContent[..120] + "..." : inputContent
                        }, category: "convert");

                        // Execute conversion (existing service call)
                        var success = _fileProcessingService.ConvertFile(file.FilePath, OutputDirectory);

                        sw.Stop();

                        if (success)
                        {
                            long outputSize = outputFilePath != null && File.Exists(outputFilePath) ? new FileInfo(outputFilePath).Length : 0;
                            scope.Info("Conversion succeeded", new
                            {
                                durationMs = sw.ElapsedMilliseconds,
                                outputFile = outputFilePath,
                                outputSize,
                                lineCount,
                                wordCount,
                                charCount
                            }, category: "convert");

                            _ = Dispatcher.UIThread.InvokeAsync(() =>
                            {
                                file.UpdateStatus("Completed");
                                file.ConversionNote = $"→ {Path.GetFileName(outputFilePath)}";
                            });
                            Interlocked.Increment(ref converted);
                        }
                        else
                        {
                            sw.Stop();
                            scope.Error("Conversion failed", new { durationMs = sw.ElapsedMilliseconds }, category: "convert");
                            _ = Dispatcher.UIThread.InvokeAsync(() =>
                            {
                                file.UpdateStatus("Error");
                                file.ConversionNote = "Conversion failed";
                            });
                            Interlocked.Increment(ref errors);
                        }
                    }
                    catch (Exception ex)
                    {
                        sw.Stop();
                        scope.Error("Unhandled exception", new
                        {
                            ex.Message,
                            ex.StackTrace,
                            durationMs = sw.ElapsedMilliseconds
                        }, category: "convert");
                        _ = Dispatcher.UIThread.InvokeAsync(() =>
                        {
                            file.UpdateStatus("Error");
                            file.ConversionNote = "Exception occurred";
                        });
                        Interlocked.Increment(ref errors);
                    }
                    finally
                    {
                        // Record highest severity for batch-level summary needs
                        var highest = scope.Result.HighestSeverity;
                        _highestSeverityByFile.AddOrUpdate(file.FileName, highest, (_, existing) => existing >= highest ? existing : highest);
                        _conversionItemLogResults.Add(scope.Result);
                    }

                    // Progress update
                    var currentCount = Interlocked.Increment(ref processedCount);
                    var progressPercentage = (int)((double)currentCount / selectedFiles.Length * 100);

                    _ = Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        ProgressValue = progressPercentage;
                    });
                });
            }, cancellationToken);

            ProgressText = $"Conversion complete: {converted} converted, {errors} errors";
        }
        catch (OperationCanceledException)
        {
            ProgressText = "Conversion operation was cancelled";
        }
        catch (Exception ex)
        {
            ProgressText = $"Error during conversion: {ex.Message}";
        }
        finally
        {
            IsProcessing = false;
            _processingCancellationTokenSource?.Dispose();
            _processingCancellationTokenSource = null;

            // Ensure logs are flushed before opening viewer
            try { Serilog.Log.CloseAndFlush(); } catch { /* ignore */ }

            // Open viewer to show the conversion run logs
            UILoggingService.ShowLogViewer(); // open viewer after batch
        }
    }

    [RelayCommand]
    private void CancelProcessing()
    {
        _processingCancellationTokenSource?.Cancel();
    }

    [RelayCommand]
    private void SelectOutputDirectory()
    {
        UpdateOutputDirectory();
    }

    private void UpdateOutputDirectory()
    {
        try
        {
            var processedDir = _settingsService.ProcessedDirectory;

            if (!string.IsNullOrEmpty(processedDir))
            {
                // Ensure the directory exists
                if (!Directory.Exists(processedDir))
                {
                    Directory.CreateDirectory(processedDir);
                }

                OutputDirectory = processedDir;
                HasOutputDirectory = true;
                ProgressText = $"Output directory: {OutputDirectory}";
            }
            else
            {
                OutputDirectory = "No workspace configured";
                HasOutputDirectory = false;
                ProgressText = "Please configure a workspace in Settings first";
            }
        }
        catch (Exception ex)
        {
            ProgressText = $"Error setting output directory: {ex.Message}";
            HasOutputDirectory = false;
        }
    }

    [RelayCommand]
    private void ToggleSelectAll()
    {
        if (!Files.Any())
        {
            SelectedFileCount = 0;
            return;
        }

        try
        {
            _isBulkSelectionUpdate = true;
            foreach (var file in Files)
            {
                file.IsSelected = SelectAll;
            }
        }
        finally
        {
            _isBulkSelectionUpdate = false;
        }

        SelectedFileCount = SelectAll ? Files.Count : 0;
    }

    [RelayCommand]
    private void ResetStatus()
    {
        foreach (var file in Files)
        {
            file.UpdateStatus("Ready");
        }
        ProgressText = "Status reset for all files";
        ProgressValue = 0;
    }

    private void OnFileSelectionChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(FileItemViewModel.IsSelected))
        {
            if (_isBulkSelectionUpdate)
            {
                return;
            }

            var selectedCount = Files.Count(f => f.IsSelected);
            SelectedFileCount = selectedCount;
            SelectAll = selectedCount == Files.Count && Files.Count > 0;
        }
    }

    private void OnWorkspaceChanged(object? sender, string? newWorkspace)
    {
        UpdateOutputDirectory();
        LoadAvailableWorkspaces();
    }

    private void LoadAvailableWorkspaces()
    {
        AvailableWorkspaces.Clear();

        foreach (var workspace in _settingsService.Workspaces)
        {
            var workspaceSelection = new WorkspaceSelectionViewModel
            {
                Workspace = workspace,
                IsSelected = false
            };
            workspaceSelection.PropertyChanged += OnWorkspaceSelectionChanged;
            AvailableWorkspaces.Add(workspaceSelection);
        }

        HasAvailableWorkspaces = AvailableWorkspaces.Count > 0;
        UpdateHasSelectedWorkspaces();

        // Debug: Update progress text to show workspace count
        if (AvailableWorkspaces.Count == 0)
        {
            ProgressText = "No workspaces configured. Please add workspaces in Settings first.";
        }
        else
        {
            ProgressText = $"Loaded {AvailableWorkspaces.Count} workspaces";
        }
    }

    private void OnWorkspaceSelectionChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(WorkspaceSelectionViewModel.IsSelected))
        {
            UpdateHasSelectedWorkspaces();

            // Debug: Show which workspace was toggled
            if (sender is WorkspaceSelectionViewModel workspace)
            {
                ProgressText = $"Workspace '{workspace.Workspace.Name}' {(workspace.IsSelected ? "selected" : "deselected")}. HasSelectedWorkspaces: {HasSelectedWorkspaces}";
            }
        }
    }

    private void UpdateHasSelectedWorkspaces()
    {
        var selectedCount = AvailableWorkspaces.Count(w => w.IsSelected);
        var newValue = selectedCount > 0;

        HasSelectedWorkspaces = newValue;

        ProgressText = $"Button should now be {(newValue ? "ENABLED" : "DISABLED")} - HasSelectedWorkspaces = {HasSelectedWorkspaces}";
    }

    // This method will be called automatically when HasSelectedWorkspaces changes
    partial void OnHasSelectedWorkspacesChanged(bool value)
    {
        OnPropertyChanged(nameof(CanCheckFilesEnabled));
        if (CheckFilesCommand is RelayCommand cmd)
        {
            cmd.NotifyCanExecuteChanged();
        }
    }

    // This method will be called automatically when IsCheckingFiles changes  
    partial void OnIsCheckingFilesChanged(bool value)
    {
        OnPropertyChanged(nameof(CanCheckFilesEnabled));
        if (CheckFilesCommand is RelayCommand cmd)
        {
            cmd.NotifyCanExecuteChanged();
        }
    }

    [RelayCommand]
    private void ToggleWorkspaceSelection(WorkspaceSelectionViewModel workspace)
    {
        // This will be called when a workspace checkbox is clicked
        UpdateHasSelectedWorkspaces();
        ProgressText = $"Workspace '{workspace.Workspace.Name}' {(workspace.IsSelected ? "selected" : "deselected")}. HasSelectedWorkspaces: {HasSelectedWorkspaces}";
    }

    [RelayCommand]
    private void SelectAllWorkspaces()
    {
        foreach (var workspace in AvailableWorkspaces)
        {
            workspace.IsSelected = true;
        }
        UpdateHasSelectedWorkspaces();
    }

    [RelayCommand]
    private void ClearWorkspaceSelection()
    {
        foreach (var workspace in AvailableWorkspaces)
        {
            workspace.IsSelected = false;
        }
        UpdateHasSelectedWorkspaces();
    }

    [RelayCommand]
    private void SortFiles(string columnName)
    {
        if (string.IsNullOrEmpty(columnName) || !Files.Any())
            return;

        // Toggle sort direction if clicking the same column
        if (SortColumn == columnName)
        {
            SortAscending = !SortAscending;
        }
        else
        {
            SortColumn = columnName;
            SortAscending = true;
        }

        ApplyFileFilter();
    }

    partial void OnFileSearchTextChanged(string value)
    {
        ApplyFileFilter();
    }

    private void ApplyFileFilter()
    {
        IEnumerable<FileItemViewModel> filtered = _allFiles;
        if (!string.IsNullOrWhiteSpace(FileSearchText))
        {
            var term = FileSearchText.Trim();
            filtered = filtered.Where(f =>
                f.FileName.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                f.WorkspaceName.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                f.Status.Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        filtered = ApplySort(filtered);

        Files.Clear();
        foreach (var file in filtered)
        {
            Files.Add(file);
        }

        var selectedCount = Files.Count(f => f.IsSelected);
        SelectedFileCount = selectedCount;
        SelectAll = Files.Count > 0 && selectedCount == Files.Count;
    }

    private IEnumerable<FileItemViewModel> ApplySort(IEnumerable<FileItemViewModel> items)
    {
        return SortColumn.ToLower() switch
        {
            "filename" => SortAscending
                ? items.OrderBy(f => f.FileName)
                : items.OrderByDescending(f => f.FileName),
            "filesize" => SortAscending
                ? items.OrderBy(f => GetFileSizeForSorting(f.FileSize))
                : items.OrderByDescending(f => GetFileSizeForSorting(f.FileSize)),
            "lastmodified" => SortAscending
                ? items.OrderBy(f => DateTime.TryParse(f.LastModified, out var date) ? date : DateTime.MinValue)
                : items.OrderByDescending(f => DateTime.TryParse(f.LastModified, out var date) ? date : DateTime.MinValue),
            "status" => SortAscending
                ? items.OrderBy(f => f.Status)
                : items.OrderByDescending(f => f.Status),
            "workspace" => SortAscending
                ? items.OrderBy(f => f.WorkspaceName)
                : items.OrderByDescending(f => f.WorkspaceName),
            _ => items
        };
    }

    private long GetFileSizeForSorting(string fileSizeText)
    {
        // Extract numeric part from "1,234 bytes" format
        if (string.IsNullOrEmpty(fileSizeText))
            return 0;

        var numericPart = fileSizeText.Split(' ')[0].Replace(",", "");
        return long.TryParse(numericPart, out var size) ? size : 0;
    }

    private static bool IsOutputBuildMatch(string outputFilePath)
    {
        try
        {
            using var stream = File.OpenRead(outputFilePath);
            using var doc = JsonDocument.Parse(stream);

            if (!doc.RootElement.TryGetProperty("ProcessingInfo", out var info))
            {
                return false;
            }

            var outputVersion = info.TryGetProperty("Version", out var versionElement)
                ? versionElement.GetString()
                : null;
            var outputHash = info.TryGetProperty("AssemblyHash", out var hashElement)
                ? hashElement.GetString()
                : null;

            var currentVersion = BuildInfo.Version;
            var currentHash = BuildInfo.AssemblyHash;

            if (!string.IsNullOrWhiteSpace(currentHash) && !string.Equals(currentHash, "unknown", StringComparison.OrdinalIgnoreCase))
            {
                return string.Equals(outputHash, currentHash, StringComparison.OrdinalIgnoreCase);
            }

            if (!string.IsNullOrWhiteSpace(currentVersion) && !string.Equals(currentVersion, "unknown", StringComparison.OrdinalIgnoreCase))
            {
                return string.Equals(outputVersion, currentVersion, StringComparison.OrdinalIgnoreCase);
            }

            return false;
        }
        catch
        {
            return false;
        }
    }
}

public partial class FileItemViewModel : ObservableObject
{
    [ObservableProperty]
    private string _fileName = string.Empty;

    [ObservableProperty]
    private string _filePath = string.Empty;

    [ObservableProperty]
    private string _fileSize = string.Empty;

    [ObservableProperty]
    private string _lastModified = string.Empty;

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private string _status = "Ready";

    [ObservableProperty]
    private string _statusColor = "#6C757D"; // Gray for ready state

    [ObservableProperty]
    private string _conversionNote = "";

    [ObservableProperty]
    private string _workspaceName = "";

    public void UpdateStatus(string status)
    {
        Status = status;
        StatusColor = status switch
        {
            "Ready" => "#6C757D",        // Gray
            "Processing" => "#FFC107",   // Yellow/Orange
            "Completed" => "#28A745",    // Green
            "Skipped" => "#17A2B8",      // Blue
            "Error" => "#DC3545",        // Red
            "Needs Update" => "#FD7E14", // Orange
            "Up to Date" => "#20C997",   // Teal/Green
            _ => "#6C757D"               // Default gray
        };
    }
}

public partial class WorkspaceSelectionViewModel : ObservableObject
{
    [ObservableProperty]
    private WorkspaceInfo _workspace = new();

    [ObservableProperty]
    private bool _isSelected;
}
