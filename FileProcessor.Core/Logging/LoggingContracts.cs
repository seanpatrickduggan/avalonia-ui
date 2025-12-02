using System.Collections.ObjectModel;

namespace FileProcessor.Core.Logging;

public enum LogSeverity
{
    Trace = 0,
    Debug = 1,
    Info = 2,
    Warning = 3,
    Error = 4,
    Critical = 5
}

public sealed record ItemLogEntry(
    DateTime TsUtc,
    LogSeverity Level,
    string Category,
    string Subcategory,
    string Message,
    object? Data);

public sealed record ItemLogResult(
    string ItemId,
    LogSeverity HighestSeverity,
    int Count,
    IReadOnlyList<ItemLogEntry> Entries,
    IReadOnlyDictionary<LogSeverity, int> LevelCounts,
    bool Truncated,
    string? SpillFilePath)
{
    public static ItemLogResult Empty(string itemId) => new(
        itemId,
        LogSeverity.Info,
        0,
        Array.Empty<ItemLogEntry>(),
        new ReadOnlyDictionary<LogSeverity, int>(new Dictionary<LogSeverity, int>()),
        false,
        null);
}

public enum ItemLogOverflowPolicy
{
    StopRecording,
    TruncateAndFlag,
    SpillToDisk
}

public sealed record ItemLogOptions(
    int InitialCapacity = 128,
    int MaxEntries = 5000,
    ItemLogOverflowPolicy Overflow = ItemLogOverflowPolicy.SpillToDisk,
    string SpillDirectory = "logs/spill");

public interface IItemLogScope : IDisposable
{
    string ItemId { get; }
    void Log(LogSeverity level, string category, string subcategory, string message, object? data = null);
    ItemLogResult Result { get; }
}

public interface IItemLogFactory
{
    IItemLogScope Start(string itemId);
}

// Operation-scoped structured logger abstraction
public interface IOperationStructuredLogger
{
    void Log(Guid operationId, string? operationType, string itemId, LogSeverity level, string category, string subcategory, string message, object? data = null);
}

public interface IOperationContext
{
    string OperationId { get; }
    string LogFilePath { get; }
    IItemLogFactory ItemLogFactory { get; }
    IOperationStructuredLogger OperationLogger { get; }

    void Initialize(string operationId, string logFilePath);
    System.Threading.Tasks.Task StartNewOperationAsync(string? operationType = null);
    System.Threading.Tasks.Task EndCurrentOperationAsync(string status = "succeeded");
}
