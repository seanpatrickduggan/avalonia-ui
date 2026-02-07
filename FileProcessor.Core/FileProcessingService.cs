using FileProcessor.Core.Interfaces;
using FileProcessor.Core.Logging;

namespace FileProcessor.Core;

public class FileProcessingService(IItemLogFactory? itemLogFactory = null) : IFileProcessingService
{
    // optional factory
    private readonly IItemLogFactory? _itemLogFactory = itemLogFactory;

    /// <summary>
    /// Processes all .txt files in the specified directory, logging details for each file using the item log factory if available.
    /// Returns a summary of the processing and a list of item log results.
    /// </summary>
    /// <param name="directoryPath">The path to the directory containing the .txt files to process.</param>
    /// <returns>A tuple containing:
    /// <list type="bullet">
    /// <item><description>A summary string describing the number of files processed and their total size in bytes.</description></item>
    /// <item><description>A read-only list of <see cref="ItemLogResult"/> objects representing the logs for each processed file.</description></item>
    /// </list>
    /// If the directory does not exist, returns ("Directory not found.", empty list).
    /// If no .txt files are found, returns ("No .txt files found to process.", empty list).
    /// </returns>
    public (string summary, IReadOnlyList<ItemLogResult> itemLogs) ProcessFilesWithLogs(string directoryPath)
    {
        if (!Directory.Exists(directoryPath))
        {
            return ("Directory not found.", Array.Empty<ItemLogResult>());
        }
        var files = Directory.GetFiles(directoryPath, "*.txt");
        if (files.Length == 0)
        {
            return ("No .txt files found to process.", Array.Empty<ItemLogResult>());
        }
        var results = new List<ItemLogResult>(files.Length);
        long totalSize = 0;
        foreach (var file in files)
        {
            var fileInfo = new FileInfo(file);
            totalSize += fileInfo.Length;
            if (_itemLogFactory != null)
            {
                using var scope = _itemLogFactory.Start(Path.GetFileName(file));
                scope.Log(LogSeverity.Info, "TXT", "Scan", $"Found file {fileInfo.Name}", new { fileInfo.Length });
                if (fileInfo.Length == 0)
                {
                    scope.Log(LogSeverity.Warning, "TXT", "Validation", "File is empty");
                }
                results.Add(scope.Result);
            }
        }
        var summary = $"Processed {files.Length} files. Total size: {totalSize} bytes.";
        return (summary, results);
    }

    public async Task<int> ProcessFilesAsync(string directoryPath)
    {
        if (!Directory.Exists(directoryPath))
        {
            return 0;
        }

        var files = Directory.GetFiles(directoryPath, "*.txt");

        int processedCount = 0;
        foreach (var file in files)
        {
            if (await ProcessFileAsync(file))
            {
                processedCount++;
            }
        }

        return processedCount;
    }

    public async Task<bool> ProcessFileAsync(string filePath)
    {
        try
        {
            // Read and validate the file
            var content = await File.ReadAllTextAsync(filePath);

            // Example processing: ensure file is not empty
            return !string.IsNullOrWhiteSpace(content);
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> ConvertFileAsync(string inputFilePath, string outputDirectory)
    {
        try
        {
            if (!File.Exists(inputFilePath))
                return false;

            // Ensure output directory exists
            if (!Directory.Exists(outputDirectory))
            {
                Directory.CreateDirectory(outputDirectory);
            }

            var fileName = Path.GetFileNameWithoutExtension(inputFilePath);
            var outputFileName = $"{fileName}_converted.json";
            var outputFilePath = Path.Combine(outputDirectory, outputFileName);

            // Read input file
            var inputContent = await File.ReadAllTextAsync(inputFilePath);
            var inputInfo = new FileInfo(inputFilePath);

            // Convert to JSON format
            var convertedData = new
            {
                OriginalFile = inputFilePath,
                OriginalSize = inputInfo.Length,
                OriginalModified = inputInfo.LastWriteTime,
                ConvertedAt = DateTime.UtcNow,
                LineCount = inputContent.Split('\n').Length,
                WordCount = inputContent.Split(new[] { ' ', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries).Length,
                CharacterCount = inputContent.Length,
                ContentPreview = inputContent.Length > 200 ? inputContent.Substring(0, 200) + "..." : inputContent,
                ProcessingInfo = new
                {
                    Processor = "FileProcessor.Core",
                    Version = BuildInfo.Version,
                    AssemblyHash = BuildInfo.AssemblyHash,
                    ProcessingTime = DateTime.UtcNow
                }
            };

            var json = System.Text.Json.JsonSerializer.Serialize(convertedData, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true
            });

            // Write converted file
            await File.WriteAllTextAsync(outputFilePath, json);

            return true;
        }
        catch
        {
            return false;
        }
    }

    public bool ConvertFile(string inputFilePath, string outputDirectory)
    {
        try
        {
            if (!File.Exists(inputFilePath))
                return false;

            // Ensure output directory exists
            if (!Directory.Exists(outputDirectory))
            {
                Directory.CreateDirectory(outputDirectory);
            }

            var fileName = Path.GetFileNameWithoutExtension(inputFilePath);
            var outputFileName = $"{fileName}_converted.json";
            var outputFilePath = Path.Combine(outputDirectory, outputFileName);

            // Read input file
            var inputContent = File.ReadAllText(inputFilePath);
            var inputInfo = new FileInfo(inputFilePath);

            // Convert to JSON format
            var convertedData = new
            {
                OriginalFile = inputFilePath,
                OriginalSize = inputInfo.Length,
                OriginalModified = inputInfo.LastWriteTime,
                ConvertedAt = DateTime.UtcNow,
                LineCount = inputContent.Split('\n').Length,
                WordCount = inputContent.Split(new[] { ' ', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries).Length,
                CharacterCount = inputContent.Length,
                ContentPreview = inputContent.Length > 200 ? inputContent.Substring(0, 200) + "..." : inputContent,
                ProcessingInfo = new
                {
                    Processor = "FileProcessor.Core",
                    Version = BuildInfo.Version,
                    AssemblyHash = BuildInfo.AssemblyHash,
                    ProcessingTime = DateTime.UtcNow
                }
            };

            var json = System.Text.Json.JsonSerializer.Serialize(convertedData, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true
            });

            // Write converted file
            File.WriteAllText(outputFilePath, json);
            try { File.SetLastWriteTimeUtc(outputFilePath, DateTime.UtcNow); } catch { }

            return true;
        }
        catch
        {
            return false;
        }
    }

    public bool NeedsConversion(string inputFilePath, string outputFilePath)
    {
        if (!File.Exists(inputFilePath))
            return false;

        if (!File.Exists(outputFilePath))
            return true; // Output doesn't exist, needs conversion

        var inputInfo = new FileInfo(inputFilePath);
        var outputInfo = new FileInfo(outputFilePath);

        // Convert if input file is newer than output file
        var inputUtc = inputInfo.LastWriteTimeUtc;
        var outputUtc = outputInfo.LastWriteTimeUtc;
        return inputUtc > outputUtc;
    }
}
