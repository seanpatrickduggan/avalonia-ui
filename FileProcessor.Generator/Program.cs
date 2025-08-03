using FileProcessor.Core;

namespace FileProcessor.Generator;
class Program
{
    static async Task Main(string[] args)
    {
        var generationService = new FileGenerationService();
        var directory = Path.Combine(Directory.GetCurrentDirectory(), "SampleFiles");
        int numberOfFiles = 10;

        Console.WriteLine($"Generating {numberOfFiles} sample files in: {directory}");
        await generationService.GenerateFilesAsync(directory, numberOfFiles);
        Console.WriteLine("File generation complete.");
    }
}
