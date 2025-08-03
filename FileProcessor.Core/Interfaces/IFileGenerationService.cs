namespace FileProcessor.Core.Interfaces;

/// <summary>
/// Interface for file generation operations
/// </summary>
public interface IFileGenerationService
{
    /// <summary>
    /// Generate test files in the specified directory
    /// </summary>
    /// <param name="outputDirectory">Directory where files will be generated</param>
    /// <param name="fileCount">Number of files to generate</param>
    /// <returns>Number of files successfully generated</returns>
    Task<int> GenerateTestFilesAsync(string outputDirectory, int fileCount);
    
    /// <summary>
    /// Generate a single test file
    /// </summary>
    /// <param name="filePath">Path where the file will be created</param>
    /// <param name="content">Content to write to the file</param>
    /// <returns>True if generation was successful</returns>
    Task<bool> GenerateFileAsync(string filePath, string content);
}
