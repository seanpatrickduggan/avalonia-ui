using FileProcessor.Core.Interfaces;

namespace FileProcessor.Core;

public class FileGenerationService : IFileGenerationService
{
    public async Task GenerateFilesAsync(string directoryPath, int numberOfFiles)
    {
        if (!Directory.Exists(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }

        for (int i = 0; i < numberOfFiles; i++)
        {
            var filePath = Path.Combine(directoryPath, $"file_{i + 1}.txt");
            var content = $"This is sample file {i + 1}. Timestamp: {DateTime.UtcNow:O}";
            await File.WriteAllTextAsync(filePath, content);
        }
    }

    public async Task<int> GenerateTestFilesAsync(string outputDirectory, int fileCount)
    {
        try
        {
            if (!Directory.Exists(outputDirectory))
            {
                Directory.CreateDirectory(outputDirectory);
            }

            int generatedCount = 0;
            for (int i = 0; i < fileCount; i++)
            {
                var filePath = Path.Combine(outputDirectory, $"file_{i + 1}.txt");
                var content = $"This is sample file {i + 1}. Timestamp: {DateTime.UtcNow:O}";
                
                if (await GenerateFileAsync(filePath, content))
                {
                    generatedCount++;
                }
            }

            return generatedCount;
        }
        catch
        {
            return 0;
        }
    }

    public async Task<bool> GenerateFileAsync(string filePath, string content)
    {
        try
        {
            await File.WriteAllTextAsync(filePath, content);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
