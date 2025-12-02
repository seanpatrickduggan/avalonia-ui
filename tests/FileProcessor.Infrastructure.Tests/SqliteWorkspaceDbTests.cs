using FileProcessor.Core.Abstractions;
using FileProcessor.Core.Workspace;
using FileProcessor.Infrastructure.Abstractions;
using FileProcessor.Infrastructure.Workspace;

using FluentAssertions;

using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Time.Testing;

using Xunit;

namespace FileProcessor.Infrastructure.Tests;

public class SqliteWorkspaceDbTests
{
    private static (string dir, string dbPath) TempWorkspace()
    {
        var dir = Path.Combine(Path.GetTempPath(), "wsdb-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var dbPath = Path.Combine(dir, "workspace.db");
        return (dir, dbPath);
    }

    [Fact]
    public async Task Can_Create_Db_Append_And_Query()
    {
        var (dir, dbPath) = TempWorkspace();
        try
        {
            IFileSystem fs = new SystemFileSystem();
            var db = new SqliteWorkspaceDb(fs, TimeProvider.System);
            await db.InitializeAsync(dbPath);

            // create session and operation to satisfy FKs
            var sessionId = await db.StartSessionAsync(appVersion: "test", userName: "tester", hostName: "localhost");
            var operationId = await db.StartOperationAsync(sessionId, type: "test-op", name: "unit");

            // Append a few logs
            await db.AppendLogAsync(new LogWrite(1, 2, "core", "a", "m1", null, sessionId, operationId, null, null));
            await db.AppendLogAsync(new LogWrite(2, 4, "core", "a", "m2", null, sessionId, operationId, null, null));
            await db.AppendLogAsync(new LogWrite(3, 3, "core", "b", "m3", null, sessionId, operationId, null, null));

            // Group counts
            var groups = await db.QueryGroupCountsAsync(new LogQuery(PageSize: 100));
            groups.Should().Contain(g => g.Category == "core" && g.Subcategory == "a" && g.Count == 2 && g.MaxLevel == 4);
            groups.Should().Contain(g => g.Category == "core" && g.Subcategory == "b" && g.Count == 1 && g.MaxLevel == 3);

            // Filtered logs
            var rows = await db.QueryLogsAsync(new LogQuery(Category: "core", Subcategory: "a", MinLevel: 3, PageSize: 100));
            rows.Should().HaveCount(1);
            rows[0].Message.Should().Be("m2");
        }
        finally
        {
            try { Directory.Delete(dir, recursive: true); } catch { }
        }
    }

    [Fact]
    public async Task Recreates_Db_When_Schema_Version_Mismatch_Writes_Notice()
    {
        var (dir, dbPath) = TempWorkspace();
        try
        {
            // 1) Create a DB with an old schema_info version
            var cs = new SqliteConnectionStringBuilder { DataSource = dbPath, Mode = SqliteOpenMode.ReadWriteCreate }.ToString();
            await using (var conn = new SqliteConnection(cs))
            {
                await conn.OpenAsync();
                await using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"PRAGMA journal_mode=WAL; PRAGMA foreign_keys=ON; CREATE TABLE IF NOT EXISTS schema_info(version INTEGER NOT NULL); DELETE FROM schema_info; INSERT INTO schema_info(version) VALUES(1);";
                    await cmd.ExecuteNonQueryAsync();
                }
            }

            var fakeTime = new FakeTimeProvider(DateTimeOffset.UtcNow);
            IFileSystem fs = new SystemFileSystem();
            var db = new SqliteWorkspaceDb(fs, fakeTime);

            // 2) Initialize should detect mismatch and recreate DB, writing a notice file
            await db.InitializeAsync(dbPath);

            var noticePath = Path.Combine(dir, "workspace-schema-reset.txt");
            File.Exists(noticePath).Should().BeTrue("notice file should be written on schema reset");
            var noticeText = await File.ReadAllTextAsync(noticePath);
            noticeText.Should().Contain("schema version mismatch");

            // 3) Verify schema_info now matches current version by letting Initialize run again (no-op)
            await db.InitializeAsync(dbPath);

            // And basic operation still works: create session
            var sessionId = await db.StartSessionAsync(appVersion: "test");
            sessionId.Should().BeGreaterThan(0);
        }
        finally
        {
            try { Directory.Delete(dir, recursive: true); } catch { }
        }
    }

    [Fact]
    public async Task Can_End_Session()
    {
        var (dir, dbPath) = TempWorkspace();
        try
        {
            IFileSystem fs = new SystemFileSystem();
            var db = new SqliteWorkspaceDb(fs, TimeProvider.System);
            await db.InitializeAsync(dbPath);

            var sessionId = await db.StartSessionAsync();
            await db.EndSessionAsync(sessionId);

            // Should not throw and session should be ended
            // We can't easily verify the end time without querying the DB directly
        }
        finally
        {
            try { Directory.Delete(dir, recursive: true); } catch { }
        }
    }

    [Fact]
    public async Task Can_End_Operation()
    {
        var (dir, dbPath) = TempWorkspace();
        try
        {
            IFileSystem fs = new SystemFileSystem();
            var db = new SqliteWorkspaceDb(fs, TimeProvider.System);
            await db.InitializeAsync(dbPath);

            var sessionId = await db.StartSessionAsync();
            var operationId = await db.StartOperationAsync(sessionId, "test-op");
            await db.EndOperationAsync(operationId, "completed");

            // Should not throw and operation should be ended
        }
        finally
        {
            try { Directory.Delete(dir, recursive: true); } catch { }
        }
    }

    [Fact]
    public async Task Can_End_Operation_With_Custom_End_Time()
    {
        var (dir, dbPath) = TempWorkspace();
        try
        {
            IFileSystem fs = new SystemFileSystem();
            var db = new SqliteWorkspaceDb(fs, TimeProvider.System);
            await db.InitializeAsync(dbPath);

            var sessionId = await db.StartSessionAsync();
            var operationId = await db.StartOperationAsync(sessionId, "test-op");
            var customEndTime = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeMilliseconds();
            await db.EndOperationAsync(operationId, "completed", customEndTime);

            // Should not throw
        }
        finally
        {
            try { Directory.Delete(dir, recursive: true); } catch { }
        }
    }

    [Fact]
    public async Task Can_Upsert_Item_New_Item()
    {
        var (dir, dbPath) = TempWorkspace();
        try
        {
            IFileSystem fs = new SystemFileSystem();
            var db = new SqliteWorkspaceDb(fs, TimeProvider.System);
            await db.InitializeAsync(dbPath);

            var sessionId = await db.StartSessionAsync();
            var operationId = await db.StartOperationAsync(sessionId, "test-op");

            var itemId = await db.UpsertItemAsync(operationId, "item1", "running", 2);
            itemId.Should().BeGreaterThan(0);

            // Upsert same item again should return same ID
            var itemId2 = await db.UpsertItemAsync(operationId, "item1", "completed", 3);
            itemId2.Should().Be(itemId);
        }
        finally
        {
            try { Directory.Delete(dir, recursive: true); } catch { }
        }
    }

    [Fact]
    public async Task Can_Upsert_Item_With_Custom_Times()
    {
        var (dir, dbPath) = TempWorkspace();
        try
        {
            IFileSystem fs = new SystemFileSystem();
            var db = new SqliteWorkspaceDb(fs, TimeProvider.System);
            await db.InitializeAsync(dbPath);

            var sessionId = await db.StartSessionAsync();
            var operationId = await db.StartOperationAsync(sessionId, "test-op");

            var startTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var endTime = startTime + 1000;

            var itemId = await db.UpsertItemAsync(operationId, "item1", "completed", 2, startTime, endTime, "{\"key\":\"value\"}");
            itemId.Should().BeGreaterThan(0);
        }
        finally
        {
            try { Directory.Delete(dir, recursive: true); } catch { }
        }
    }
}
