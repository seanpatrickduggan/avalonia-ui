using FileProcessor.Core.Interfaces;

namespace FileProcessor.Core;

public class FileProcessingService : IFileProcessingService
{
    public string ProcessFiles(string directoryPath)
    {
        if (!Directory.Exists(directoryPath))
        {
            return "Directory not found.";
        }

        var files = Directory.GetFiles(directoryPath, "*.txt");
        if (files.Length == 0)
        {
            return "No .txt files found to process.";
        }

        long totalSize = 0;
        foreach (var file in files)
        {
            var fileInfo = new FileInfo(file);
            totalSize += fileInfo.Length;
        }

        return $"Processed {files.Length} files. Total size: {totalSize} bytes.";
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
            // Simulate some processing work
            await Task.Delay(100);
            
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
}
