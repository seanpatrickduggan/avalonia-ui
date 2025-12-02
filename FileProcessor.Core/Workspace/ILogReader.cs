namespace FileProcessor.Core.Workspace;

// Abstraction for reading logs from different backends (SQLite, JSONL, etc.)
public interface ILogReader
{
    Task<IReadOnlyList<LogRow>> QueryLogsAsync(LogQuery query, CancellationToken ct = default);
    Task<IReadOnlyList<LogGroupCount>> QueryGroupCountsAsync(LogQuery query, CancellationToken ct = default);
}

public interface ILogReaderFactory
{
    ILogReader ForDatabase();
    ILogReader ForJsonl(string filePath);
}
