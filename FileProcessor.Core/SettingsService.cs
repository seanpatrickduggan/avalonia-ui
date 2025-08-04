using FileProcessor.Core.Interfaces;
using System.Text.Json;

namespace FileProcessor.Core;

public class SettingsService : ISettingsService
{
    private static SettingsService? _instance;
    private static readonly object _lock = new object();
    
    private readonly string _settingsFilePath;
    private AppSettings _settings;

    public event EventHandler<string?>? WorkspaceChanged;

    public static SettingsService Instance
    {
        get
        {
            if (_instance == null)
            {
                lock (_lock)
                {
                    if (_instance == null)
                        _instance = new SettingsService();
                }
            }
            return _instance;
        }
    }

    private SettingsService()
    {
        var settingsDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FileProcessor");
        if (!Directory.Exists(settingsDirectory))
        {
            Directory.CreateDirectory(settingsDirectory);
        }
        
        _settingsFilePath = Path.Combine(settingsDirectory, "settings.json");
        _settings = new AppSettings();
        
        // Load settings synchronously during initialization to avoid deadlocks
        LoadSettingsSync();
    }

    private void LoadSettingsSync()
    {
        try
        {
            if (File.Exists(_settingsFilePath))
            {
                var json = File.ReadAllText(_settingsFilePath);
                var settings = JsonSerializer.Deserialize<AppSettings>(json);
                if (settings != null)
                {
                    _settings = settings;
                    
                    // Set WorkspaceDirectory from ActiveWorkspace if available
                    if (!string.IsNullOrEmpty(_settings.ActiveWorkspace))
                    {
                        WorkspaceDirectory = _settings.ActiveWorkspace;
                        
                        // Set IsActive flag on the current workspace
                        foreach (var workspace in _settings.Workspaces)
                        {
                            workspace.IsActive = workspace.Path.Equals(_settings.ActiveWorkspace, 
                                StringComparison.OrdinalIgnoreCase);
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            // Log error but continue with default settings
            Console.WriteLine($"Failed to load settings: {ex.Message}");
            _settings = new AppSettings();
        }
    }

    public string? WorkspaceDirectory 
    { 
        get => _settings.ActiveWorkspace;
        set 
        {
            if (_settings.ActiveWorkspace != value)
            {
                _settings.ActiveWorkspace = value;
                WorkspaceChanged?.Invoke(this, value);
            }
        }
    }

    public List<WorkspaceInfo> Workspaces => _settings.Workspaces;

    public string? InputDirectory => 
        string.IsNullOrEmpty(WorkspaceDirectory) ? null : Path.Combine(WorkspaceDirectory, "input");

    public string? ProcessedDirectory => 
        string.IsNullOrEmpty(WorkspaceDirectory) ? null : Path.Combine(WorkspaceDirectory, "processed");

    public int CoreSpareCount 
    { 
        get => _settings.CoreSpareCount; 
        set 
        {
            if (_settings.CoreSpareCount != value)
            {
                _settings.CoreSpareCount = Math.Max(0, Math.Min(value, Environment.ProcessorCount - 1));
                _ = SaveSettingsAsync();
            }
        }
    }

    public int MaxDegreeOfParallelism => Math.Max(1, Environment.ProcessorCount - CoreSpareCount);

    public async Task SaveSettingsAsync()
    {
        try
        {
            // Update ActiveWorkspace before saving
            _settings.ActiveWorkspace = WorkspaceDirectory;
            
            var json = JsonSerializer.Serialize(_settings, new JsonSerializerOptions
            {
                WriteIndented = true
            });
            
            await File.WriteAllTextAsync(_settingsFilePath, json);
        }
        catch (Exception ex)
        {
            // Log error but don't throw - settings save failure shouldn't crash the app
            Console.WriteLine($"Failed to save settings: {ex.Message}");
        }
    }

    public async Task LoadSettingsAsync()
    {
        try
        {
            if (File.Exists(_settingsFilePath))
            {
                var json = await File.ReadAllTextAsync(_settingsFilePath);
                var settings = JsonSerializer.Deserialize<AppSettings>(json);
                if (settings != null)
                {
                    _settings = settings;
                    
                    // Set WorkspaceDirectory from ActiveWorkspace if available
                    if (!string.IsNullOrEmpty(_settings.ActiveWorkspace))
                    {
                        WorkspaceDirectory = _settings.ActiveWorkspace;
                        
                        // Set IsActive flag on the current workspace
                        foreach (var workspace in _settings.Workspaces)
                        {
                            workspace.IsActive = workspace.Path.Equals(_settings.ActiveWorkspace, 
                                StringComparison.OrdinalIgnoreCase);
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            // Log error but continue with default settings
            Console.WriteLine($"Failed to load settings: {ex.Message}");
            _settings = new AppSettings();
        }
    }

    public async Task<bool> AddWorkspaceAsync(string workspacePath, string? name = null)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(workspacePath))
            {
                return false;
            }

            // Normalize the path
            workspacePath = Path.GetFullPath(workspacePath);

            // Check if workspace already exists
            if (_settings.Workspaces.Any(w => w.Path.Equals(workspacePath, StringComparison.OrdinalIgnoreCase)))
            {
                // Already exists, just set as active
                return await SetActiveWorkspaceAsync(workspacePath);
            }

            // Ensure the workspace directory exists
            if (!Directory.Exists(workspacePath))
            {
                Directory.CreateDirectory(workspacePath);
            }

            // Create input and processed subdirectories
            var inputDir = Path.Combine(workspacePath, "input");
            var processedDir = Path.Combine(workspacePath, "processed");

            if (!Directory.Exists(inputDir))
            {
                Directory.CreateDirectory(inputDir);
            }

            if (!Directory.Exists(processedDir))
            {
                Directory.CreateDirectory(processedDir);
            }

            // Add to workspace list
            var workspaceName = name ?? Path.GetFileName(workspacePath) ?? "Unnamed Workspace";
            _settings.Workspaces.Add(new WorkspaceInfo
            {
                Path = workspacePath,
                Name = workspaceName,
                CreatedDate = DateTime.Now
            });

            // Set as active workspace
            WorkspaceDirectory = workspacePath;
            
            // Save the updated settings
            await SaveSettingsAsync();

            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to add workspace: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> SetActiveWorkspaceAsync(string workspacePath)
    {
        try
        {
            var workspace = _settings.Workspaces.FirstOrDefault(w => 
                w.Path.Equals(workspacePath, StringComparison.OrdinalIgnoreCase));
            
            if (workspace != null && Directory.Exists(workspace.Path))
            {
                // Clear previous active workspace
                foreach (var w in _settings.Workspaces)
                {
                    w.IsActive = false;
                }

                // Set new active workspace
                workspace.IsActive = true;
                WorkspaceDirectory = workspace.Path;
                await SaveSettingsAsync();
                WorkspaceChanged?.Invoke(this, workspace.Path);
                return true;
            }
            
            return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to set active workspace: {ex.Message}");
            return false;
        }
    }

    public async Task SetActiveWorkspaceAsync(WorkspaceInfo workspace)
    {
        if (workspace == null) return;

        // Clear previous active workspace
        foreach (var w in _settings.Workspaces)
        {
            w.IsActive = false;
        }

        // Set new active workspace
        workspace.IsActive = true;
        WorkspaceDirectory = workspace.Path;
        await SaveSettingsAsync();
        WorkspaceChanged?.Invoke(this, workspace.Path);
    }

    public async Task<bool> RemoveWorkspaceAsync(string workspacePath)
    {
        try
        {
            var workspace = _settings.Workspaces.FirstOrDefault(w => 
                w.Path.Equals(workspacePath, StringComparison.OrdinalIgnoreCase));
            
            if (workspace != null)
            {
                _settings.Workspaces.Remove(workspace);
                
                // If this was the active workspace, clear it
                if (WorkspaceDirectory?.Equals(workspacePath, StringComparison.OrdinalIgnoreCase) == true)
                {
                    WorkspaceDirectory = _settings.Workspaces.FirstOrDefault()?.Path;
                }
                
                await SaveSettingsAsync();
                return true;
            }
            
            return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to remove workspace: {ex.Message}");
            return false;
        }
    }

    public async Task RemoveWorkspaceAsync(WorkspaceInfo workspace)
    {
        if (workspace == null) return;
        await RemoveWorkspaceAsync(workspace.Path);
    }
}

public class AppSettings
{
    public string? ActiveWorkspace { get; set; }
    public List<WorkspaceInfo> Workspaces { get; set; } = new();
    public string? Theme { get; set; } = "Dark";
    public bool AutoSaveEnabled { get; set; } = true;
    public int CoreSpareCount { get; set; } = 1; // Number of cores to spare during processing
}
