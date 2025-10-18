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
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Threading;
using FileProcessor.Infrastructure.Logging;
using FileProcessor.UI.Services; // added
using FileProcessor.Core.Logging; // added for scoping
using Serilog;

namespace FileProcessor.UI.ViewModels;

public partial class FileConverterViewModel : ViewModelBase
{
    private readonly FileProcessingService _fileProcessingService;
    private readonly SettingsService _settingsService;
    private CancellationTokenSource? _checkCancellationTokenSource;
    private CancellationTokenSource? _processingCancellationTokenSource;

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

    // Computed property for check button enabled state
    public bool CanCheckFilesEnabled 
    { 
        get 
        {
            var result = HasSelectedWorkspaces && !IsCheckingFiles;
            Console.WriteLine($"CanCheckFilesEnabled getter called: HasSelectedWorkspaces={HasSelectedWorkspaces}, IsCheckingFiles={IsCheckingFiles}, Result={result}");
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

    public FileConverterViewModel()
    {
        _fileProcessingService = new FileProcessingService();
        _settingsService = SettingsService.Instance;
        
        // Create manual command with explicit CanExecute control
        CheckFilesCommand = new RelayCommand(
            execute: async () => await CheckFilesAsync(),
            canExecute: () => {
                var canExecute = HasSelectedWorkspaces && !IsCheckingFiles;
                Console.WriteLine($"CheckFilesCommand.CanExecute called: {canExecute}");
                return canExecute;
            });
        
        // Subscribe to workspace changes
        _settingsService.WorkspaceChanged += OnWorkspaceChanged;

        // Debug initial command state
        Console.WriteLine("Constructor: Command created");
        
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
            
            var parallelOptions = new ParallelOptions
            {
                CancellationToken = cancellationToken,
                MaxDegreeOfParallelism = _settingsService.MaxDegreeOfParallelism
            };

            await Task.Run(() =>
            {
                Parallel.ForEach(allFiles, parallelOptions, file =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    
                    // Generate expected output file path
                    var outputFileName = Path.GetFileNameWithoutExtension(file.FilePath) + "_converted.json";
                    var outputFilePath = Path.Combine(OutputDirectory, outputFileName);
                    
                    // Check if conversion is needed
                    bool needsUpdate = _fileProcessingService.NeedsConversion(file.FilePath, outputFilePath);
                    
                    // Update file properties (thread-safe)
                    if (needsUpdate)
                    {
                        file.IsSelected = true;
                        file.UpdateStatus("Needs Update");
                        file.ConversionNote = File.Exists(outputFilePath) ? "Output outdated" : "Not converted";
                        Interlocked.Increment(ref needsConversion);
                    }
                    else
                    {
                        file.IsSelected = false;
                        file.UpdateStatus("Up to Date");
                        file.ConversionNote = "Current";
                        Interlocked.Increment(ref upToDate);
                    }
                    
                    var currentCount = Interlocked.Increment(ref processedCount);
                    
                    // Update UI progress on main thread less frequently (every 10 files)
                    if (currentCount % 10 == 0 || currentCount == allFiles.Count)
                    {
                        _ = Dispatcher.UIThread.InvokeAsync(() =>
                        {
                            CheckProgressText = $"Checking file {currentCount} of {allFiles.Count}...";
                        });
                    }
                });
            }, cancellationToken);
            
            // Add all files to the UI collection
            foreach (var fileItem in allFiles)
            {
                fileItem.PropertyChanged += OnFileSelectionChanged;
                Files.Add(fileItem);
            }
            
            // Update SelectAll state based on selections
            var selectedCount = Files.Count(f => f.IsSelected);
            SelectedFileCount = selectedCount;
            SelectAll = selectedCount == Files.Count;
            
            ProgressText = $"Check complete: {allFiles.Count} total files, {needsConversion} need conversion, {upToDate} up to date, {selectedCount} selected";
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
        LoggingService.StartNewRun("conversion");

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
                    using var scope = LoggingService.ItemLogFactory.Start(file.FileName);
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
        foreach (var file in Files)
        {
            file.IsSelected = SelectAll;
        }
        
        // Update selected count
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
            // Update SelectAll state based on individual file selections
            var allSelected = Files.All(f => f.IsSelected);
            var noneSelected = Files.All(f => !f.IsSelected);
            
            if (allSelected)
            {
                SelectAll = true;
            }
            else if (noneSelected)
            {
                SelectAll = false;
            }
            
            // Update selected count
            SelectedFileCount = Files.Count(f => f.IsSelected);
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
        
        // Debug output
        Console.WriteLine($"UpdateHasSelectedWorkspaces: {selectedCount} selected out of {AvailableWorkspaces.Count} total. New value: {newValue}, Current value: {HasSelectedWorkspaces}");
        
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
            Console.WriteLine($"OnHasSelectedWorkspacesChanged: {value}, notified command CanExecute changed");
        }
        Console.WriteLine($"OnHasSelectedWorkspacesChanged: {value}, CanCheckFilesEnabled: {CanCheckFilesEnabled}");
    }

    // This method will be called automatically when IsCheckingFiles changes  
    partial void OnIsCheckingFilesChanged(bool value)
    {
        OnPropertyChanged(nameof(CanCheckFilesEnabled));
        if (CheckFilesCommand is RelayCommand cmd)
        {
            cmd.NotifyCanExecuteChanged();
            Console.WriteLine($"OnIsCheckingFilesChanged: {value}, notified command CanExecute changed");
        }
        Console.WriteLine($"OnIsCheckingFilesChanged: {value}, CanCheckFilesEnabled: {CanCheckFilesEnabled}");
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

        // Create a sorted list
        IEnumerable<FileItemViewModel> sortedFiles = columnName.ToLower() switch
        {
            "filename" => SortAscending 
                ? Files.OrderBy(f => f.FileName) 
                : Files.OrderByDescending(f => f.FileName),
            "filesize" => SortAscending 
                ? Files.OrderBy(f => GetFileSizeForSorting(f.FileSize)) 
                : Files.OrderByDescending(f => GetFileSizeForSorting(f.FileSize)),
            "lastmodified" => SortAscending 
                ? Files.OrderBy(f => DateTime.TryParse(f.LastModified, out var date) ? date : DateTime.MinValue) 
                : Files.OrderByDescending(f => DateTime.TryParse(f.LastModified, out var date) ? date : DateTime.MinValue),
            "status" => SortAscending 
                ? Files.OrderBy(f => f.Status) 
                : Files.OrderByDescending(f => f.Status),
            "workspace" => SortAscending 
                ? Files.OrderBy(f => f.WorkspaceName) 
                : Files.OrderByDescending(f => f.WorkspaceName),
            _ => Files
        };

        // Clear and re-add sorted items
        var sortedList = sortedFiles.ToList();
        Files.Clear();
        foreach (var file in sortedList)
        {
            Files.Add(file);
        }
    }

    private long GetFileSizeForSorting(string fileSizeText)
    {
        // Extract numeric part from "1,234 bytes" format
        if (string.IsNullOrEmpty(fileSizeText))
            return 0;

        var numericPart = fileSizeText.Split(' ')[0].Replace(",", "");
        return long.TryParse(numericPart, out var size) ? size : 0;
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
