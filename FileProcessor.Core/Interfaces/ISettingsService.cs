namespace FileProcessor.Core.Interfaces;

/// <summary>
/// Interface for application settings management
/// </summary>
public interface ISettingsService
{
    /// <summary>
    /// Gets the current workspace root directory
    /// </summary>
    string? WorkspaceDirectory { get; set; }
    
    /// <summary>
    /// Gets the list of all configured workspaces
    /// </summary>
    List<WorkspaceInfo> Workspaces { get; }
    
    /// <summary>
    /// Gets the input folder path within the workspace
    /// </summary>
    string? InputDirectory { get; }
    
    /// <summary>
    /// Gets the processed folder path within the workspace
    /// </summary>
    string? ProcessedDirectory { get; }
    
    /// <summary>
    /// Gets or sets the number of CPU cores to spare during processing
    /// </summary>
    int CoreSpareCount { get; set; }

    /// <summary>
    /// Gets the maximum degree of parallelism based on available cores and spare count
    /// </summary>
    int MaxDegreeOfParallelism { get; }
    
    /// <summary>
    /// Event raised when the workspace directory changes
    /// </summary>
    event EventHandler<string?>? WorkspaceChanged;
    
    /// <summary>
    /// Saves the current settings to persistent storage
    /// </summary>
    Task SaveSettingsAsync();
    
    /// <summary>
    /// Loads settings from persistent storage
    /// </summary>
    Task LoadSettingsAsync();
    
    /// <summary>
    /// Validates and creates a workspace directory structure
    /// </summary>
    /// <param name="workspacePath">Path to the workspace directory</param>
    /// <param name="name">Display name for the workspace</param>
    /// <returns>True if workspace is valid and ready to use</returns>
    Task<bool> AddWorkspaceAsync(string workspacePath, string? name = null);
    
    /// <summary>
    /// Sets the active workspace
    /// </summary>
    /// <param name="workspacePath">Path to the workspace to activate</param>
    /// <returns>True if workspace was successfully activated</returns>
    Task<bool> SetActiveWorkspaceAsync(string workspacePath);
    
    /// <summary>
    /// Sets the active workspace
    /// </summary>
    /// <param name="workspace">Workspace to activate</param>
    Task SetActiveWorkspaceAsync(WorkspaceInfo workspace);
    
    /// <summary>
    /// Removes a workspace from the list
    /// </summary>
    /// <param name="workspacePath">Path to the workspace to remove</param>
    /// <returns>True if workspace was successfully removed</returns>
    Task<bool> RemoveWorkspaceAsync(string workspacePath);
    
    /// <summary>
    /// Removes a workspace from the list
    /// </summary>
    /// <param name="workspace">Workspace to remove</param>
    Task RemoveWorkspaceAsync(WorkspaceInfo workspace);
}

/// <summary>
/// Information about a workspace
/// </summary>
    public class WorkspaceInfo
    {
        public string Path { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public DateTime CreatedDate { get; set; } = DateTime.Now;
        public bool IsValid => !string.IsNullOrEmpty(Path) && Directory.Exists(Path);
        public bool IsActive { get; set; }
    }
