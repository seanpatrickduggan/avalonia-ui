namespace FileProcessor.Core.Interfaces;

/// <summary>
/// Interface for file processing operations
/// </summary>
public interface IFileProcessingService
{
    /// <summary>
    /// Process files in the specified directory
    /// </summary>
    /// <param name="directoryPath">Path to the directory containing files to process</param>
    /// <returns>Number of files processed</returns>
    Task<int> ProcessFilesAsync(string directoryPath);
    
    /// <summary>
    /// Process a single file
    /// </summary>
    /// <param name="filePath">Path to the file to process</param>
    /// <returns>True if processing was successful</returns>
    Task<bool> ProcessFileAsync(string filePath);
}
