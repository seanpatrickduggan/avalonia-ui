using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FileProcessor.Core;
using FluentAssertions;
using Xunit;

namespace FileProcessor.Core.Tests;

public class FileGenerationServiceTests : IDisposable
{
    private readonly string _tempDir;

    public FileGenerationServiceTests()
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
    public async Task GenerateFilesAsync_Creates_Directory_And_Files()
    {
        var service = new FileGenerationService();
        var subDir = Path.Combine(_tempDir, "test");

        await service.GenerateFilesAsync(subDir, 3);

        Directory.Exists(subDir).Should().BeTrue();
        var files = Directory.GetFiles(subDir);
        files.Length.Should().Be(3);
        files.All(f => Path.GetFileName(f).StartsWith("file_")).Should().BeTrue();
    }

    [Fact]
    public async Task GenerateFilesAsync_With_Progress_Reports_Correctly()
    {
        var service = new FileGenerationService();
        var subDir = Path.Combine(_tempDir, "progress");
        var progressReports = new System.Collections.Generic.List<(int completed, int total)>();

        var progress = new Progress<(int completed, int total)>(p => progressReports.Add(p));

        await service.GenerateFilesAsync(subDir, 5, progress);

        progressReports.Should().HaveCount(2); // every 100 or last, but since <100, only last
        progressReports.Last().Should().Be((5, 5));
    }

    [Fact]
    public async Task GenerateTestFilesAsync_Creates_Files_And_Returns_Count()
    {
        var service = new FileGenerationService();
        var subDir = Path.Combine(_tempDir, "testfiles");

        var count = await service.GenerateTestFilesAsync(subDir, 2);

        count.Should().Be(2);
        var files = Directory.GetFiles(subDir);
        files.Length.Should().Be(2);
        files.All(f => Path.GetFileName(f).StartsWith("test_file_")).Should().BeTrue();
    }

    [Fact]
    public async Task GenerateTestFilesAsync_With_Progress_Reports_Correctly()
    {
        var service = new FileGenerationService();
        var subDir = Path.Combine(_tempDir, "testprogress");
        var progressReports = new System.Collections.Generic.List<(int completed, int total)>();

        var progress = new Progress<(int completed, int total)>(p => progressReports.Add(p));

        var count = await service.GenerateTestFilesAsync(subDir, 3, progress);

        count.Should().Be(3);
        progressReports.Should().HaveCount(1); // only on last
        progressReports.Last().Should().Be((3, 3));
    }

    [Fact]
    public async Task GenerateFileAsync_Writes_Content()
    {
        var service = new FileGenerationService();
        var filePath = Path.Combine(_tempDir, "single.txt");
        var content = "test content";

        var result = await service.GenerateFileAsync(filePath, content);

        result.Should().BeTrue();
        File.Exists(filePath).Should().BeTrue();
        var actualContent = await File.ReadAllTextAsync(filePath);
        actualContent.Should().Be(content);
    }

    [Fact]
    public async Task GenerateFileAsync_Returns_False_On_Error()
    {
        var service = new FileGenerationService();
        var invalidPath = Path.Combine("invalid", "path", "file.txt");

        var result = await service.GenerateFileAsync(invalidPath, "content");

        result.Should().BeFalse();
    }

    [Fact]
    public async Task GenerateTestFilesAsync_Returns_Zero_On_Exception()
    {
        var service = new FileGenerationService();
        // Pass invalid path to trigger exception
        var count = await service.GenerateTestFilesAsync("", 1);
        count.Should().Be(0);
    }
}
