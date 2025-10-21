using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FileProcessor.Core;
using FileProcessor.Core.Logging;
using FluentAssertions;
using Xunit;

namespace FileProcessor.Core.Tests;

public class FileProcessingServiceTests : IDisposable
{
    private readonly string _tempDir;

    public FileProcessingServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    [Fact]
    public void ProcessFilesWithLogs_DirectoryNotFound_ReturnsMessage()
    {
        var service = new FileProcessingService();
        var (summary, logs) = service.ProcessFilesWithLogs("/nonexistent");
        summary.Should().Be("Directory not found.");
        logs.Should().BeEmpty();
    }

    [Fact]
    public void ProcessFilesWithLogs_NoTxtFiles_ReturnsMessage()
    {
        var service = new FileProcessingService();
        var (summary, logs) = service.ProcessFilesWithLogs(_tempDir);
        summary.Should().Be("No .txt files found to process.");
        logs.Should().BeEmpty();
    }

    [Fact]
    public void ProcessFilesWithLogs_WithFiles_NoFactory_ReturnsSummary()
    {
        var service = new FileProcessingService();
        var file1 = Path.Combine(_tempDir, "test1.txt");
        var file2 = Path.Combine(_tempDir, "test2.txt");
        File.WriteAllText(file1, "content1");
        File.WriteAllText(file2, "content2");

        var (summary, logs) = service.ProcessFilesWithLogs(_tempDir);
        summary.Should().Contain("Processed 2 files");
        summary.Should().Contain("Total size:");
        logs.Should().BeEmpty();
    }

    [Fact]
    public void ProcessFilesWithLogs_WithFiles_WithFactory_ReturnsLogs()
    {
        var fakeFactory = new FakeItemLogFactory();
        var service = new FileProcessingService(fakeFactory);
        var file1 = Path.Combine(_tempDir, "test1.txt");
        var file2 = Path.Combine(_tempDir, "empty.txt");
        File.WriteAllText(file1, "content1");
        File.WriteAllText(file2, ""); // empty file

        var (summary, logs) = service.ProcessFilesWithLogs(_tempDir);
        summary.Should().Contain("Processed 2 files");
        logs.Should().HaveCount(2);
        // Order of files returned by the filesystem is not guaranteed; assert the IDs regardless of order.
        logs.Select(l => l.ItemId).Should().BeEquivalentTo(new[] { "test1.txt", "empty.txt" });
    }

    [Fact]
    public async Task ProcessFilesAsync_NoDirectory_ReturnsZero()
    {
        var service = new FileProcessingService();
        var count = await service.ProcessFilesAsync("/nonexistent");
        count.Should().Be(0);
    }

    [Fact]
    public async Task ProcessFilesAsync_WithFiles_ReturnsCount()
    {
        var service = new FileProcessingService();
        var file1 = Path.Combine(_tempDir, "test1.txt");
        var file2 = Path.Combine(_tempDir, "test2.txt");
        File.WriteAllText(file1, "content1");
        File.WriteAllText(file2, ""); // empty, should not count

        var count = await service.ProcessFilesAsync(_tempDir);
        count.Should().Be(1); // only non-empty
    }

    [Fact]
    public async Task ProcessFileAsync_ValidFile_ReturnsTrue()
    {
        var service = new FileProcessingService();
        var file = Path.Combine(_tempDir, "test.txt");
        File.WriteAllText(file, "content");

        var result = await service.ProcessFileAsync(file);
        result.Should().BeTrue();
    }

    [Fact]
    public async Task ProcessFileAsync_EmptyFile_ReturnsFalse()
    {
        var service = new FileProcessingService();
        var file = Path.Combine(_tempDir, "empty.txt");
        File.WriteAllText(file, "");

        var result = await service.ProcessFileAsync(file);
        result.Should().BeFalse();
    }

    [Fact]
    public async Task ProcessFileAsync_InvalidFile_ReturnsFalse()
    {
        var service = new FileProcessingService();
        var result = await service.ProcessFileAsync("/nonexistent.txt");
        result.Should().BeFalse();
    }

    [Fact]
    public async Task ConvertFileAsync_ValidFile_CreatesJson()
    {
        var service = new FileProcessingService();
        var inputFile = Path.Combine(_tempDir, "input.txt");
        var outputDir = Path.Combine(_tempDir, "output");
        File.WriteAllText(inputFile, "line1\nline2\nword1 word2");

        var result = await service.ConvertFileAsync(inputFile, outputDir);
        result.Should().BeTrue();

        var outputFile = Path.Combine(outputDir, "input_converted.json");
        File.Exists(outputFile).Should().BeTrue();
        var json = File.ReadAllText(outputFile);
        json.Should().Contain("input.txt");
        json.Should().Contain("LineCount");
    }

    [Fact]
    public async Task ConvertFileAsync_InvalidFile_ReturnsFalse()
    {
        var service = new FileProcessingService();
        var result = await service.ConvertFileAsync("/nonexistent.txt", _tempDir);
        result.Should().BeFalse();
    }

    [Fact]
    public void ConvertFile_ValidFile_CreatesJson()
    {
        var service = new FileProcessingService();
        var inputFile = Path.Combine(_tempDir, "input.txt");
        var outputDir = Path.Combine(_tempDir, "output");
        File.WriteAllText(inputFile, "line1\nline2\nword1 word2");

        var result = service.ConvertFile(inputFile, outputDir);
        result.Should().BeTrue();

        var outputFile = Path.Combine(outputDir, "input_converted.json");
        File.Exists(outputFile).Should().BeTrue();
    }

    [Fact]
    public void ConvertFile_InvalidOutputDirectory_ReturnsFalse()
    {
        var service = new FileProcessingService();
        var inputFile = Path.Combine(_tempDir, "input.txt");
        File.WriteAllText(inputFile, "content");

        var result = service.ConvertFile(inputFile, "");
        result.Should().BeFalse();
    }

    [Fact]
    public void NeedsConversion_OutputNotExists_ReturnsTrue()
    {
        var service = new FileProcessingService();
        var inputFile = Path.Combine(_tempDir, "input.txt");
        var outputFile = Path.Combine(_tempDir, "output.json");
        File.WriteAllText(inputFile, "content");

        var result = service.NeedsConversion(inputFile, outputFile);
        result.Should().BeTrue();
    }

    [Fact]
    public void NeedsConversion_InputNewer_ReturnsTrue()
    {
        var service = new FileProcessingService();
        var inputFile = Path.Combine(_tempDir, "input.txt");
        var outputFile = Path.Combine(_tempDir, "output.json");
        File.WriteAllText(inputFile, "content");
        File.WriteAllText(outputFile, "old");

        // Set input newer
        File.SetLastWriteTime(inputFile, DateTime.Now.AddMinutes(1));

        var result = service.NeedsConversion(inputFile, outputFile);
        result.Should().BeTrue();
    }

    [Fact]
    public void NeedsConversion_OutputNewer_ReturnsFalse()
    {
        var service = new FileProcessingService();
        var inputFile = Path.Combine(_tempDir, "input.txt");
        var outputFile = Path.Combine(_tempDir, "output.json");
        File.WriteAllText(inputFile, "content");
        File.WriteAllText(outputFile, "new");

        // Set output newer
        File.SetLastWriteTime(outputFile, DateTime.Now.AddMinutes(1));

        var result = service.NeedsConversion(inputFile, outputFile);
        result.Should().BeFalse();
    }

    [Fact]
    public async Task ConvertFileAsync_LongContent_PreviewsCorrectly()
    {
        var service = new FileProcessingService();
        var inputFile = Path.Combine(_tempDir, "long.txt");
        var outputDir = Path.Combine(_tempDir, "output");
        var longContent = new string('a', 300) + " end";
        File.WriteAllText(inputFile, longContent);

        var result = await service.ConvertFileAsync(inputFile, outputDir);
        result.Should().BeTrue();

        var outputFile = Path.Combine(outputDir, "long_converted.json");
        var json = File.ReadAllText(outputFile);
        json.Should().Contain("ContentPreview");
        json.Should().Contain(new string('a', 200) + "...");
    }

    [Fact]
    public async Task ConvertFileAsync_InvalidOutputDirectory_ReturnsFalse()
    {
        var service = new FileProcessingService();
        var inputFile = Path.Combine(_tempDir, "input.txt");
        File.WriteAllText(inputFile, "content");

        var result = await service.ConvertFileAsync(inputFile, "");
        result.Should().BeFalse();
    }

    [Fact]
    public async Task ConvertFileAsync_ShortContent_NoTruncation()
    {
        var service = new FileProcessingService();
        var inputFile = Path.Combine(_tempDir, "short.txt");
        var outputDir = Path.Combine(_tempDir, "output");
        var shortContent = "short content";
        File.WriteAllText(inputFile, shortContent);

        var result = await service.ConvertFileAsync(inputFile, outputDir);
        result.Should().BeTrue();

        var outputFile = Path.Combine(outputDir, "short_converted.json");
        var json = File.ReadAllText(outputFile);
        json.Should().Contain("ContentPreview");
        json.Should().Contain(shortContent);
    }

    private sealed class FakeItemLogFactory : IItemLogFactory
    {
        public IItemLogScope Start(string itemId) => new FakeScope(itemId);
    }

    private sealed class FakeScope : IItemLogScope
    {
        private readonly List<(LogSeverity severity, string category, string subcategory, string message, object? data)> _logs = new();
        public string ItemId { get; }

        public FakeScope(string itemId)
        {
            ItemId = itemId;
        }

        public void Log(LogSeverity severity, string category, string subcategory, string message, object? data = null)
        {
            _logs.Add((severity, category, subcategory, message, data));
        }

        public ItemLogResult Result
        {
            get
            {
                var entries = _logs.Select((log, i) => new ItemLogEntry(DateTime.Now, log.severity, log.category, log.subcategory, log.message, log.data)).ToArray();
                var levelCounts = _logs.GroupBy(l => l.severity).ToDictionary(g => g.Key, g => g.Count());
                return new ItemLogResult(ItemId, LogSeverity.Info, _logs.Count, entries, levelCounts, false, null);
            }
        }

        public void Dispose() { }
    }
}
