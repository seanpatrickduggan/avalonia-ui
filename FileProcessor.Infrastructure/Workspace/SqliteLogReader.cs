using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FileProcessor.Core.Workspace;

namespace FileProcessor.Infrastructure.Workspace;

public sealed class SqliteLogReader : ILogReader
{
    private readonly IWorkspaceDb _db;
    public SqliteLogReader(IWorkspaceDb db) => _db = db;

    public Task<IReadOnlyList<LogRow>> QueryLogsAsync(LogQuery query, CancellationToken ct = default)
        => _db.QueryLogsAsync(query, ct);

    public Task<IReadOnlyList<LogGroupCount>> QueryGroupCountsAsync(LogQuery query, CancellationToken ct = default)
        => _db.QueryGroupCountsAsync(query, ct);
}
