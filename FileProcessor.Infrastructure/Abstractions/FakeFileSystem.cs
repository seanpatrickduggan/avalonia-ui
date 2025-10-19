using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FileProcessor.Core.Abstractions;

namespace FileProcessor.Infrastructure.Abstractions;

// In-memory file system for tests
public sealed class FakeFileSystem : IFileSystem
{
    private readonly HashSet<string> _dirs = new();
    private readonly Dictionary<string, byte[]> _files = new();

    public void CreateDirectory(string path) => _dirs.Add(Norm(path));
    public bool FileExists(string path) => _files.ContainsKey(Norm(path));

    public Stream CreateFile(string path, FileMode mode, FileAccess access, FileShare share)
    {
        path = Norm(path);
        if (mode == FileMode.Create || mode == FileMode.CreateNew || mode == FileMode.Truncate)
        {
            _files[path] = new byte[0];
        }
        if (!_files.TryGetValue(path, out var bytes))
        {
            bytes = new byte[0];
            _files[path] = bytes;
        }
        return new MemoryStreamWrapper(this, path, new MemoryStream(bytes, writable: true));
    }

    public Task WriteAllTextAsync(string path, string contents, CancellationToken ct = default)
    {
        path = Norm(path);
        _files[path] = Encoding.UTF8.GetBytes(contents ?? string.Empty);
        return Task.CompletedTask;
    }

    public Task<string> ReadAllTextAsync(string path, CancellationToken ct = default)
    {
        path = Norm(path);
        if (!_files.TryGetValue(path, out var bytes)) return Task.FromResult(string.Empty);
        return Task.FromResult(Encoding.UTF8.GetString(bytes));
    }

    public void DeleteFile(string path)
    {
        path = Norm(path);
        _files.Remove(path);
    }

    private static string Norm(string p) => p.Replace('\\', '/');

    private sealed class MemoryStreamWrapper : MemoryStream
    {
        private readonly FakeFileSystem _fs;
        private readonly string _path;
        public MemoryStreamWrapper(FakeFileSystem fs, string path, MemoryStream inner) : base(inner.ToArray(), true)
        {
            _fs = fs; _path = path;
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _fs._files[_path] = ToArray();
            }
            base.Dispose(disposing);
        }
    }
}
