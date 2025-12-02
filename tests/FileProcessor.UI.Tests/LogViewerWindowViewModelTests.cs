using Avalonia.Headless.XUnit;
using FluentAssertions;
using FileProcessor.UI.ViewModels;
using FileProcessor.Core.Workspace;
using FileProcessor.Core.Logging;

namespace FileProcessor.UI.Tests;

public class LogViewerWindowViewModelTests
{
    private sealed class FakeRuntime : IWorkspaceRuntime
    {
        public long SessionId { get; set; }
        public long CurrentOperationId { get; set; }
        public Task InitializeAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task<long> StartOperationAsync(string type, string? name = null, string? metadataJson = null, long startedAtMs = 0, CancellationToken ct = default) => Task.FromResult(0L);
        public Task EndOperationAsync(string status = "succeeded", long endedAtMs = 0, CancellationToken ct = default) => Task.CompletedTask;
        public Task<string> MaterializeOperationLogsAsync(long operationId, string? outputPath = null, CancellationToken ct = default) => Task.FromResult(string.Empty);
        public Task<string> MaterializeSessionLogsAsync(long sessionId, string? outputPath = null, CancellationToken ct = default) => Task.FromResult(string.Empty);
        public Task ShutdownAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task AppendOrBufferAsync(LogWrite write, CancellationToken ct = default) => Task.CompletedTask;
        public Task FlushAsync(CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class FakeOpLogger : IOperationStructuredLogger
    {
        public void Log(Guid operationId, string? operationType, string itemId, LogSeverity level, string category, string subcategory, string message, object? data = null) { }
    }

    private sealed class FakeItemLogFactory : IItemLogFactory
    {
        private sealed class NullScope : IItemLogScope
        {
            public string ItemId { get; }
            public NullScope(string itemId) => ItemId = itemId;
            public void Log(LogSeverity level, string category, string subcategory, string message, object? data = null) { }
            public ItemLogResult Result => ItemLogResult.Empty(ItemId);
            public void Dispose() { }
        }
        public IItemLogScope Start(string itemId) => new NullScope(itemId);
    }

    private sealed class FakeOpContext : IOperationContext
    {
        public string OperationId { get; private set; } = Guid.NewGuid().ToString("N");
        public string LogFilePath { get; private set; } = string.Empty;
        public IItemLogFactory ItemLogFactory { get; } = new FakeItemLogFactory();
        public IOperationStructuredLogger OperationLogger { get; } = new FakeOpLogger();
        public void Initialize(string operationId, string logFilePath) { OperationId = operationId; LogFilePath = logFilePath; }
        public Task StartNewOperationAsync(string? operationType = null) => Task.CompletedTask;
        public Task EndCurrentOperationAsync(string status = "succeeded") => Task.CompletedTask;
    }

    private sealed class CapturingDbReader : ILogReader
    {
        public LogQuery? LastQuery { get; private set; }
        public Task<IReadOnlyList<LogRow>> QueryLogsAsync(LogQuery query, CancellationToken ct = default)
        {
            LastQuery = query;
            return Task.FromResult<IReadOnlyList<LogRow>>(new List<LogRow>());
        }
        public Task<IReadOnlyList<LogGroupCount>> QueryGroupCountsAsync(LogQuery query, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<LogGroupCount>>(new List<LogGroupCount>());
    }

    private sealed class FakeReaderFactory : ILogReaderFactory
    {
        private readonly ILogReader _db;
        public FakeReaderFactory(ILogReader db) { _db = db; }
        public ILogReader ForDatabase() => _db;
        public ILogReader ForJsonl(string filePath) => new FileProcessor.Infrastructure.Workspace.JsonlLogReader(filePath);
    }

    private static async Task WaitUntilAsync(Func<bool> condition, int timeoutMs = 1500, int pollMs = 20)
    {
        var start = DateTime.UtcNow;
        while (!condition())
        {
            if ((DateTime.UtcNow - start).TotalMilliseconds > timeoutMs)
                break;
            await Task.Delay(pollMs);
        }
    }

    [AvaloniaFact]
    public async Task Uses_Runtime_Ids_In_Db_Query()
    {
        var runtime = new FakeRuntime { SessionId = 111, CurrentOperationId = 222 };
        var op = new FakeOpContext();
        op.Initialize(Guid.NewGuid().ToString("N"), "/tmp/does-not-exist.jsonl");
        var capturing = new CapturingDbReader();
        var factory = new FakeReaderFactory(capturing);

        using var vm = new LogViewerWindowViewModel(runtime, op, factory);
        // Wait for initial async DB query to be issued
        await WaitUntilAsync(() => capturing.LastQuery is not null);

        capturing.LastQuery.Should().NotBeNull();
        capturing.LastQuery!.OperationId.Should().Be(runtime.CurrentOperationId);
        capturing.LastQuery!.SessionId.Should().BeNull();
    }

    [AvaloniaFact]
    public async Task Loads_Jsonl_And_Filters()
    {
        // arrange a temp jsonl file with two entries
        var tmp = Path.GetTempFileName();
        try
        {
            await File.WriteAllLinesAsync(tmp, new[]
            {
                "{ \"@t\": \"2024-01-01T12:00:00Z\", \"@l\": \"Information\", \"@mt\": \"Hello world\", \"Properties\": { \"cat\": \"core\", \"sub\": \"init\" } }",
                "{ \"@t\": \"2024-01-01T12:00:01Z\", \"@l\": \"Error\", \"Message\": \"Boom\", \"Properties\": { \"cat\": \"core\", \"sub\": \"run\" } }"
            });

            var runtime = new FakeRuntime { SessionId = 1, CurrentOperationId = 0 }; // no active op => session mode
            var op = new FakeOpContext();
            op.Initialize(Guid.NewGuid().ToString("N"), tmp);
            var capturing = new CapturingDbReader();
            var factory = new FakeReaderFactory(capturing);

            using var vm = new LogViewerWindowViewModel(runtime, op, factory);
            vm.UseDatabase = false; // switch to file mode and load initial (synchronous)

            vm.Entries.Count.Should().Be(2);
            vm.Categories.Should().Contain("core");

            vm.SelectedCategory = "core";
            vm.SelectedSubcategory = "run";
            // Debounce timer is 250ms; wait until it applies
            await WaitUntilAsync(() => vm.Entries.Count == 1);

            vm.Entries.Count.Should().Be(1);
            vm.Groups.Count.Should().Be(1);
            vm.Groups[0].HighestSeverity.Should().Be(LogSeverity.Error);
        }
        finally
        {
            try { File.Delete(tmp); } catch { }
        }
    }
}
