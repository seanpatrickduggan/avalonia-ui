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

    /// <summary>
    /// Convert a file to the output directory
    /// </summary>
    /// <param name="inputFilePath">Source file path</param>
    /// <param name="outputDirectory">Target directory for converted file</param>
    /// <returns>True if conversion was successful</returns>
    Task<bool> ConvertFileAsync(string inputFilePath, string outputDirectory);

    /// <summary>
    /// Convert a file to the output directory (synchronous for parallel processing)
    /// </summary>
    /// <param name="inputFilePath">Source file path</param>
    /// <param name="outputDirectory">Target directory for converted file</param>
    /// <returns>True if conversion was successful</returns>
    bool ConvertFile(string inputFilePath, string outputDirectory);

    /// <summary>
    /// Check if a file needs conversion based on timestamps
    /// </summary>
    /// <param name="inputFilePath">Source file path</param>
    /// <param name="outputFilePath">Target file path</param>
    /// <returns>True if conversion is needed</returns>
    bool NeedsConversion(string inputFilePath, string outputFilePath);
}
