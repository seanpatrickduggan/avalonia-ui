using FileProcessor.Core.Workspace;
using FileProcessor.Core.Logging;

namespace LogViewer.UI.Services;

internal sealed class ContextAwareLogReaderFactory : ILogReaderFactory
{
    private readonly IOperationContext _opContext;

    public ContextAwareLogReaderFactory(IOperationContext opContext)
    {
        _opContext = opContext;
    }

    public ILogReader ForDatabase()
    {
        var path = _opContext.LogFilePath;
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return new EmptyLogReader();
        return new JsonlLogReader(path);
    }

    public ILogReader ForJsonl(string filePath) => File.Exists(filePath) ? new JsonlLogReader(filePath) : new EmptyLogReader();

    private sealed class EmptyLogReader : ILogReader
    {
        public Task<IReadOnlyList<LogGroupCount>> QueryGroupCountsAsync(LogQuery query, CancellationToken ct = default) => Task.FromResult((IReadOnlyList<LogGroupCount>)Array.Empty<LogGroupCount>());
        public Task<IReadOnlyList<LogRow>> QueryLogsAsync(LogQuery query, CancellationToken ct = default) => Task.FromResult((IReadOnlyList<LogRow>)Array.Empty<LogRow>());
    }

    private sealed class JsonlLogReader : ILogReader
    {
        private readonly string _path;
        public JsonlLogReader(string path) => _path = path;

        public Task<IReadOnlyList<LogGroupCount>> QueryGroupCountsAsync(LogQuery query, CancellationToken ct = default)
        {
            return Task.FromResult((IReadOnlyList<LogGroupCount>)Array.Empty<LogGroupCount>());
        }

        public async Task<IReadOnlyList<LogRow>> QueryLogsAsync(LogQuery query, CancellationToken ct = default)
        {
            var list = new List<LogRow>();
            if (!File.Exists(_path)) return list;
            using var sr = new StreamReader(_path);
            string? line;
            while ((line = await sr.ReadLineAsync().ConfigureAwait(false)) != null)
            {
                var entry = FileProcessor.Core.Logging.LogParser.Parse(line);
                if (entry == null) continue;
                var ts = new System.DateTimeOffset(entry.TsUtc).ToUnixTimeMilliseconds();
                if (query.FromTsMs.HasValue && ts < query.FromTsMs.Value) continue;
                var row = new LogRow(0, ts, (int)entry.Level, entry.Category, entry.Subcategory, entry.Message, entry.Data as string, null, null);
                list.Add(row);
            }
            return list;
        }
    }
}
