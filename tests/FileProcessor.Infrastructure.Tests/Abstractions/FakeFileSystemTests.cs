using System.IO;
using System.Text;
using System.Threading.Tasks;
using FileProcessor.Core.Abstractions;
using FileProcessor.Infrastructure.Abstractions;
using FluentAssertions;
using Xunit;

namespace FileProcessor.Infrastructure.Tests.Abstractions;

public class FakeFileSystemTests
{
    private readonly FakeFileSystem _fs = new();

    [Fact]
    public void CreateDirectory_AddsToDirsSet()
    {
        _fs.CreateDirectory("/test/dir");
        // Can't directly test internal state, but we can test behavior
        _fs.FileExists("/test/dir/file").Should().BeFalse(); // Directory creation doesn't affect files
    }

    [Fact]
    public void FileExists_ReturnsFalseForNonExistentFile()
    {
        _fs.FileExists("/nonexistent").Should().BeFalse();
    }

    [Fact]
    public void FileExists_ReturnsTrueForExistingFile()
    {
        using var stream = _fs.CreateFile("/test.txt", FileMode.Create, FileAccess.Write, FileShare.None);
        _fs.FileExists("/test.txt").Should().BeTrue();
    }

    [Fact]
    public void CreateFile_FileModeCreate_CreatesEmptyFile()
    {
        using var stream = _fs.CreateFile("/test.txt", FileMode.Create, FileAccess.Write, FileShare.None);
        _fs.FileExists("/test.txt").Should().BeTrue();
    }

    [Fact]
    public void CreateFile_FileModeCreateNew_CreatesEmptyFile()
    {
        using var stream = _fs.CreateFile("/test.txt", FileMode.CreateNew, FileAccess.Write, FileShare.None);
        _fs.FileExists("/test.txt").Should().BeTrue();
    }

    [Fact]
    public void CreateFile_FileModeTruncate_CreatesEmptyFile()
    {
        // First create a file with content
        using (var stream = _fs.CreateFile("/test.txt", FileMode.Create, FileAccess.Write, FileShare.None))
        {
            var data = Encoding.UTF8.GetBytes("content");
            stream.Write(data, 0, data.Length);
            stream.Flush();
        }

        // Then truncate it
        using var truncatedStream = _fs.CreateFile("/test.txt", FileMode.Truncate, FileAccess.Write, FileShare.None);
        truncatedStream.Length.Should().Be(0);
    }

    [Fact]
    public void CreateFile_FileModeOpen_UsesExistingFile()
    {
        // First create a file with content
        using (var stream = _fs.CreateFile("/test.txt", FileMode.Create, FileAccess.Write, FileShare.None))
        {
            var data = Encoding.UTF8.GetBytes("existing content");
            stream.Write(data, 0, data.Length);
            stream.Flush();
        }

        // Then open it
        using var openStream = _fs.CreateFile("/test.txt", FileMode.Open, FileAccess.Read, FileShare.None);
        var buffer = new byte[openStream.Length];
        openStream.Read(buffer, 0, buffer.Length);
        var content = Encoding.UTF8.GetString(buffer);
        content.Should().Be("existing content");
    }

    [Fact]
    public async Task WriteAllTextAsync_StoresContentAsUtf8()
    {
        await _fs.WriteAllTextAsync("/test.txt", "Hello World");
        _fs.FileExists("/test.txt").Should().BeTrue();

        var readContent = await _fs.ReadAllTextAsync("/test.txt");
        readContent.Should().Be("Hello World");
    }

    [Fact]
    public async Task WriteAllTextAsync_NullContent_StoresEmptyString()
    {
        await _fs.WriteAllTextAsync("/test.txt", null!);
        var content = await _fs.ReadAllTextAsync("/test.txt");
        content.Should().Be("");
    }

    [Fact]
    public async Task ReadAllTextAsync_NonExistentFile_ReturnsEmptyString()
    {
        var content = await _fs.ReadAllTextAsync("/nonexistent.txt");
        content.Should().Be("");
    }

    [Fact]
    public async Task ReadAllTextAsync_ExistingFile_ReturnsContent()
    {
        await _fs.WriteAllTextAsync("/test.txt", "Test Content");
        var content = await _fs.ReadAllTextAsync("/test.txt");
        content.Should().Be("Test Content");
    }

    [Fact]
    public void DeleteFile_RemovesFile()
    {
        using (_fs.CreateFile("/test.txt", FileMode.Create, FileAccess.Write, FileShare.None)) { }
        _fs.FileExists("/test.txt").Should().BeTrue();

        _fs.DeleteFile("/test.txt");
        _fs.FileExists("/test.txt").Should().BeFalse();
    }

    [Fact]
    public void DeleteFile_NonExistentFile_DoesNothing()
    {
        // Should not throw
        _fs.DeleteFile("/nonexistent.txt");
    }

    [Fact]
    public async Task MemoryStreamWrapper_Dispose_SavesContent()
    {
        using (var stream = _fs.CreateFile("/test.txt", FileMode.Create, FileAccess.Write, FileShare.None))
        {
            var data = Encoding.UTF8.GetBytes("Saved Content");
            stream.Write(data, 0, data.Length);
            stream.Flush();
            // Dispose should save the content
        }

        // Content should be saved after dispose
        var content = await _fs.ReadAllTextAsync("/test.txt");
        content.Should().Be("Saved Content");
    }

    [Fact]
    public void Norm_NormalizesPathSeparators()
    {
        // We can't directly test the private Norm method, but we can test its effect
        // by using paths with backslashes and verifying they work the same as forward slashes
        _fs.CreateDirectory("\\test\\dir");
        using (_fs.CreateFile("\\test\\dir\\file.txt", FileMode.Create, FileAccess.Write, FileShare.None)) { }

        _fs.FileExists("/test/dir/file.txt").Should().BeTrue();
        _fs.FileExists("\\test\\dir\\file.txt").Should().BeTrue();
    }

    [Fact]
    public void CreateFile_FileModeOpenOrCreate_CreatesFileWhenNotExists()
    {
        using var stream = _fs.CreateFile("/test.txt", FileMode.OpenOrCreate, FileAccess.Write, FileShare.None);
        _fs.FileExists("/test.txt").Should().BeTrue();
    }
}
