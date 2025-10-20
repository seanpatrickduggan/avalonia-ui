using System;
using System.IO;

namespace FileProcessor.Tests;

public sealed class TempWorkspace : IDisposable
{
    public string Root { get; }
    public string Input => Path.Combine(Root, "input");
    public string Processed => Path.Combine(Root, "processed");
    public string Logs => Path.Combine(Root, "logs");

    public TempWorkspace()
    {
        Root = Path.Combine(Path.GetTempPath(), "FileProcessorTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Input);
        Directory.CreateDirectory(Processed);
        Directory.CreateDirectory(Logs);
    }

    public void Dispose()
    {
        try { Directory.Delete(Root, recursive: true); } catch { }
    }
}
