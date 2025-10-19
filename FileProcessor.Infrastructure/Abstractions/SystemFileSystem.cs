using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FileProcessor.Core.Abstractions;

namespace FileProcessor.Infrastructure.Abstractions;

public sealed class SystemFileSystem : IFileSystem
{
    public void CreateDirectory(string path) => Directory.CreateDirectory(path);
    public bool FileExists(string path) => File.Exists(path);
    public Stream CreateFile(string path, FileMode mode, FileAccess access, FileShare share) => new FileStream(path, mode, access, share);
    public Task WriteAllTextAsync(string path, string contents, CancellationToken ct = default) => File.WriteAllTextAsync(path, contents, ct);
    public Task<string> ReadAllTextAsync(string path, CancellationToken ct = default) => File.ReadAllTextAsync(path, ct);
    public void DeleteFile(string path) { if (File.Exists(path)) File.Delete(path); }
}
