using FileProcessor.Core.Abstractions;
using FileProcessor.Core.Workspace;
using FileProcessor.Infrastructure.Abstractions;
using FileProcessor.Infrastructure.Workspace;

using FluentAssertions;

using Xunit;

namespace FileProcessor.Infrastructure.Tests;

public class SqliteLogReaderTests
{
    private static (string dir, string dbPath) TempWorkspace()
    {
        var dir = Path.Combine(Path.GetTempPath(), "sldb-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var dbPath = Path.Combine(dir, "workspace.db");
        return (dir, dbPath);
    }

    [Fact]
    public async Task QueryLogs_Works_Via_Reader()
    {
        var (dir, dbPath) = TempWorkspace();
        try
        {
            IFileSystem fs = new SystemFileSystem();
            using var db = new SqliteWorkspaceDb(fs, TimeProvider.System);
            await db.InitializeAsync(dbPath);

            var sessionId = await db.StartSessionAsync(appVersion: "test");
            var opId = await db.StartOperationAsync(sessionId, type: "op", name: "n");

            await db.AppendLogAsync(new LogWrite(1, 2, "core", "a", "m1", null, sessionId, opId, null, null));
            await db.AppendLogAsync(new LogWrite(2, 4, "core", "a", "m2", null, sessionId, opId, null, null));
            await db.AppendLogAsync(new LogWrite(3, 3, "core", "b", "m3", null, sessionId, opId, null, null));

            var reader = new SqliteLogReader(db);
            var rows = await reader.QueryLogsAsync(new LogQuery(Category: "core", Subcategory: "a", MinLevel: 3, PageSize: 100));
            rows.Should().HaveCount(1);
            rows[0].Message.Should().Be("m2");
        }
        finally
        {
            try { Directory.Delete(dir, recursive: true); } catch { }
        }
    }

    [Fact]
    public async Task QueryGroupCounts_Works_Via_Reader()
    {
        var (dir, dbPath) = TempWorkspace();
        try
        {
            IFileSystem fs = new SystemFileSystem();
            using var db = new SqliteWorkspaceDb(fs, TimeProvider.System);
            await db.InitializeAsync(dbPath);

            var sessionId = await db.StartSessionAsync(appVersion: "test");
            var opId = await db.StartOperationAsync(sessionId, type: "op", name: "n");

            await db.AppendLogAsync(new LogWrite(1, 2, "core", "a", "m1", null, sessionId, opId, null, null));
            await db.AppendLogAsync(new LogWrite(2, 4, "core", "a", "m2", null, sessionId, opId, null, null));
            await db.AppendLogAsync(new LogWrite(3, 3, "core", "b", "m3", null, sessionId, opId, null, null));

            var reader = new SqliteLogReader(db);
            var groups = await reader.QueryGroupCountsAsync(new LogQuery(PageSize: 100));
            groups.Should().Contain(g => g.Category == "core" && g.Subcategory == "a" && g.Count == 2 && g.MaxLevel == 4);
            groups.Should().Contain(g => g.Category == "core" && g.Subcategory == "b" && g.Count == 1 && g.MaxLevel == 3);
        }
        finally
        {
            try { Directory.Delete(dir, recursive: true); } catch { }
        }
    }
}
