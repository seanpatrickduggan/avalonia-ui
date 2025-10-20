using FileProcessor.Core.Interfaces;

namespace FileProcessor.Core;

public class FileGenerationService : IFileGenerationService
{
    public async Task GenerateFilesAsync(string directoryPath, int numberOfFiles)
    {
        await GenerateFilesAsync(directoryPath, numberOfFiles, null);
    }

    public async Task GenerateFilesAsync(string directoryPath, int numberOfFiles, IProgress<(int completed, int total)>? progress)
    {
        if (!Directory.Exists(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }

        for (int i = 0; i < numberOfFiles; i++)
        {
            var filePath = Path.Combine(directoryPath, $"file_{i + 1}.txt");
            var contentSize = Random.Shared.Next(100, 5000);
            var content = GenerateRandomContent($"file_{i + 1}.txt", contentSize, i + 1);
            await File.WriteAllTextAsync(filePath, content);
            
            // Report progress every 100 files or on the last file
            if (progress != null && (i % 100 == 0 || i == numberOfFiles - 1))
            {
                progress.Report((i + 1, numberOfFiles));
            }
        }
    }

    public async Task<int> GenerateTestFilesAsync(string outputDirectory, int fileCount)
    {
        return await GenerateTestFilesAsync(outputDirectory, fileCount, null);
    }

    public async Task<int> GenerateTestFilesAsync(string outputDirectory, int fileCount, IProgress<(int completed, int total)>? progress)
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
                var fileName = $"test_file_{i + 1:D6}.txt";
                var filePath = Path.Combine(outputDirectory, fileName);
                
                // Create varied content with different sizes
                var contentSize = Random.Shared.Next(100, 5000); // Random size between 100-5000 chars
                var content = GenerateRandomContent(fileName, contentSize, i + 1);
                
                if (await GenerateFileAsync(filePath, content))
                {
                    generatedCount++;
                }
                
                // Report progress every 100 files or on the last file
                if (progress != null && i == fileCount - 1)
                {
                    progress.Report((generatedCount, fileCount));
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

    private string GenerateRandomContent(string fileName, int size, int fileNumber)
    {
        var lines = new List<string>
        {
            $"File: {fileName}",
            $"Generated: {DateTime.UtcNow:O}",
            $"File Number: {fileNumber}",
            $"Size Target: {size} characters",
            "",
            "Sample Data Content:",
            "==================="
        };

        var random = new Random(fileNumber); // Seed with file number for consistency
        var words = new[] 
        { 
            "data", "processing", "file", "content", "sample", "test", "information", 
            "system", "application", "service", "method", "function", "variable",
            "parameter", "result", "output", "input", "configuration", "settings"
        };

        var currentLength = string.Join("\n", lines).Length;
        
        while (currentLength < size - 50) // Leave some buffer
        {
            var sentence = "";
            var sentenceLength = random.Next(5, 15); // 5-15 words per sentence
            
            for (int i = 0; i < sentenceLength; i++)
            {
                if (i > 0) sentence += " ";
                sentence += words[random.Next(words.Length)];
            }
            
            sentence += ".";
            lines.Add(sentence);
            currentLength += sentence.Length + 1; // +1 for newline
        }

        lines.Add("");
        lines.Add($"End of file {fileNumber}");
        
        return string.Join("\n", lines);
    }
}
