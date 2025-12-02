namespace FileProcessor.Core.Abstractions;

public interface IFileSystem
{
    void CreateDirectory(string path);
    bool FileExists(string path);
    Stream CreateFile(string path, FileMode mode, FileAccess access, FileShare share);
    Task WriteAllTextAsync(string path, string contents, CancellationToken ct = default);
    Task<string> ReadAllTextAsync(string path, CancellationToken ct = default);
    void DeleteFile(string path);
}
