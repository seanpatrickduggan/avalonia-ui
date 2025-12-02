using System.Text;
using FileProcessor.Infrastructure.Workspace;
using FileProcessor.Core.Workspace;
using FluentAssertions;
using Xunit;

namespace FileProcessor.Infrastructure.Tests;

public class JsonlLogReaderTests
{
    private static string MakeLine(long tsMs, int level, string category, string subcategory, string message, long? opId = null, long? itemId = null, string? dataJson = null)
    {
        var sb = new StringBuilder();
        sb.Append('{')
          .Append("\"ts_ms\":").Append(tsMs).Append(',')
          .Append("\"level\":").Append(level).Append(',')
          .Append("\"category\":\"").Append(category).Append("\",")
          .Append("\"subcategory\":\"").Append(subcategory).Append("\",")
          .Append("\"message\":\"").Append(message).Append("\"");
        if (opId is not null) sb.Append(',').Append("\"operation_id\":").Append(opId.Value);
        if (itemId is not null) sb.Append(',').Append("\"item_id\":").Append(itemId.Value);
        if (!string.IsNullOrEmpty(dataJson)) sb.Append(',').Append("\"data\":").Append(dataJson);
        sb.Append('}');
        return sb.ToString();
    }

    private static async Task<string> CreateTempJsonlAsync(params string[] lines)
    {
        var path = Path.GetTempFileName();
        await File.WriteAllLinesAsync(path, lines);
        return path;
    }

    [Fact]
    public async Task Returns_All_When_No_Filters()
    {
        var f = await CreateTempJsonlAsync(
            MakeLine(1, 2, "core", "init", "hello"),
            MakeLine(2, 3, "core", "run", "warn"));
        try
        {
            var reader = new JsonlLogReader(f);
            var rows = await reader.QueryLogsAsync(new LogQuery(PageSize: 100));
            rows.Should().HaveCount(2);
            rows.Select(r => r.Message).Should().Contain(new[] { "hello", "warn" });
        }
        finally { try { File.Delete(f); } catch { } }
    }

    [Fact]
    public async Task Applies_Category_And_Text_Filters()
    {
        var f = await CreateTempJsonlAsync(
            MakeLine(10, 2, "core", "init", "first"),
            MakeLine(20, 4, "infra", "io", "second boom"));
        try
        {
            var reader = new JsonlLogReader(f);
            var q = new LogQuery(Category: "infra", TextContains: "boom", PageSize: 10);
            var rows = await reader.QueryLogsAsync(q);
            rows.Should().HaveCount(1);
            rows[0].Category.Should().Be("infra");
            rows[0].Message.Should().Contain("boom");
        }
        finally { try { File.Delete(f); } catch { } }
    }

    [Fact]
    public async Task Respects_Level_And_Time_Range()
    {
        var f = await CreateTempJsonlAsync(
            MakeLine(1000, 2, "core", "init", "info"),
            MakeLine(2000, 4, "core", "run", "error"));
        try
        {
            var reader = new JsonlLogReader(f);
            var q = new LogQuery(MinLevel: 3, FromTsMs: 1500, ToTsMs: 2500, PageSize: 10);
            var rows = await reader.QueryLogsAsync(q);
            rows.Should().HaveCount(1);
            rows[0].Level.Should().Be(4);
            rows[0].TsMs.Should().Be(2000);
        }
        finally { try { File.Delete(f); } catch { } }
    }

    [Fact]
    public async Task Supports_Paging()
    {
        var f = await CreateTempJsonlAsync(
            MakeLine(1, 2, "core", "a", "m1"),
            MakeLine(2, 2, "core", "b", "m2"),
            MakeLine(3, 2, "core", "c", "m3"));
        try
        {
            var reader = new JsonlLogReader(f);
            var p0 = await reader.QueryLogsAsync(new LogQuery(Page: 0, PageSize: 1));
            var p1 = await reader.QueryLogsAsync(new LogQuery(Page: 1, PageSize: 1));
            p0.Should().HaveCount(1);
            p1.Should().HaveCount(1);
            p0[0].Message.Should().Be("m1");
            p1[0].Message.Should().Be("m2");
        }
        finally { try { File.Delete(f); } catch { } }
    }

    [Fact]
    public async Task Groups_By_Category_Subcategory()
    {
        var f = await CreateTempJsonlAsync(
            MakeLine(1, 2, "core", "a", "m1"),
            MakeLine(2, 4, "core", "a", "m2"),
            MakeLine(3, 3, "core", "b", "m3"));
        try
        {
            var reader = new JsonlLogReader(f);
            var groups = await reader.QueryGroupCountsAsync(new LogQuery(PageSize: 100));
            groups.Should().Contain(g => g.Category == "core" && g.Subcategory == "a" && g.Count == 2 && g.MaxLevel == 4);
            groups.Should().Contain(g => g.Category == "core" && g.Subcategory == "b" && g.Count == 1 && g.MaxLevel == 3);
        }
        finally { try { File.Delete(f); } catch { } }
    }

    [Fact]
    public async Task Honors_Cancellation()
    {
        var f = await CreateTempJsonlAsync(
            MakeLine(1, 2, "core", "a", "m1"),
            MakeLine(2, 2, "core", "b", "m2"));
        try
        {
            var reader = new JsonlLogReader(f);
            using var cts = new CancellationTokenSource();
            cts.Cancel();
            var rows = await reader.QueryLogsAsync(new LogQuery(PageSize: 100), cts.Token);
            rows.Should().BeEmpty();
        }
        finally { try { File.Delete(f); } catch { } }
    }

    [Fact]
    public async Task Skips_Invalid_Json_Lines()
    {
        var f = await CreateTempJsonlAsync("{not json}", MakeLine(1, 2, "core", "a", "ok"));
        try
        {
            var reader = new JsonlLogReader(f);
            var rows = await reader.QueryLogsAsync(new LogQuery(PageSize: 100));
            rows.Should().HaveCount(1);
            rows[0].Message.Should().Be("ok");
        }
        finally { try { File.Delete(f); } catch { } }
    }

    [Fact]
    public async Task Filters_By_Operation_And_Item()
    {
        var f = await CreateTempJsonlAsync(
            MakeLine(1, 2, "core", "a", "m1", opId: 5, itemId: 7),
            MakeLine(2, 2, "core", "a", "m2", opId: 6, itemId: 8));
        try
        {
            var reader = new JsonlLogReader(f);
            var rows = await reader.QueryLogsAsync(new LogQuery(OperationId: 5, ItemId: 7, PageSize: 100));
            rows.Should().HaveCount(1);
            rows[0].Message.Should().Be("m1");
        }
        finally { try { File.Delete(f); } catch { } }
    }

    [Fact]
    public async Task Level_And_Time_Bounds_Are_Inclusive()
    {
        var f = await CreateTempJsonlAsync(MakeLine(1000, 3, "core", "a", "m"));
        try
        {
            var reader = new JsonlLogReader(f);
            var rows = await reader.QueryLogsAsync(new LogQuery(MinLevel: 3, MaxLevel: 3, FromTsMs: 1000, ToTsMs: 1000, PageSize: 10));
            rows.Should().HaveCount(1);
        }
        finally { try { File.Delete(f); } catch { } }
    }

    [Fact]
    public async Task Text_Filter_Is_Case_Insensitive()
    {
        var f = await CreateTempJsonlAsync(MakeLine(1, 2, "core", "a", "Hello World"));
        try
        {
            var reader = new JsonlLogReader(f);
            var rows = await reader.QueryLogsAsync(new LogQuery(TextContains: "WORLD", PageSize: 10));
            rows.Should().HaveCount(1);
        }
        finally { try { File.Delete(f); } catch { } }
    }

    [Fact]
    public async Task Returns_Empty_When_File_Does_Not_Exist_QueryLogsAsync()
    {
        var path = Path.Combine(Path.GetTempPath(), "no_such_file_" + Guid.NewGuid().ToString("N") + ".jsonl");
        var reader = new JsonlLogReader(path);
        var rows = await reader.QueryLogsAsync(new LogQuery(PageSize: 10));
        rows.Should().BeEmpty();
    }

    [Fact]
    public async Task Returns_Empty_When_File_Does_Not_Exist_QueryGroupCountsAsync()
    {
        var path = Path.Combine(Path.GetTempPath(), "no_such_file_" + Guid.NewGuid().ToString("N") + ".jsonl");
        var reader = new JsonlLogReader(path);
        var groups = await reader.QueryGroupCountsAsync(new LogQuery(PageSize: 10));
        groups.Should().BeEmpty();
    }
}
