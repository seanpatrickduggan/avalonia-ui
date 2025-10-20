using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using FileProcessor.Core.Logging;
using FluentAssertions;
using Xunit;

namespace FileProcessor.Core.Tests;

public class ItemLoggerTests
{
    private sealed class CapturingOpLogger : IOperationStructuredLogger
    {
        public record Rec(Guid OpId, string? OpType, string ItemId, LogSeverity Level, string Cat, string Sub, string Msg, object? Data);
        public System.Collections.Concurrent.ConcurrentBag<Rec> Recs { get; } = new();
        public void Log(Guid operationId, string? operationType, string itemId, LogSeverity level, string category, string subcategory, string message, object? data = null)
            => Recs.Add(new Rec(operationId, operationType, itemId, level, category, subcategory, message, data));
    }

    [Fact]
    public void Records_And_Forwards_To_OperationLogger()
    {
        var opId = Guid.NewGuid();
        var opType = "op";
        var cap = new CapturingOpLogger();
        var opts = new ItemLogOptions(InitialCapacity: 2, MaxEntries: 10, Overflow: ItemLogOverflowPolicy.StopRecording, SpillDirectory: Path.GetTempPath());
        var fac = new ItemLogFactory(opts, cap, () => opId, () => opType);
        using var scope = fac.Start("it1");

        scope.Log(LogSeverity.Info, "c", "s", "m1");
        scope.Log(LogSeverity.Warning, "c", "s", "m2");

        var res = scope.Result;
        res.ItemId.Should().Be("it1");
        res.Count.Should().Be(2);
        res.HighestSeverity.Should().Be(LogSeverity.Warning);
        cap.Recs.Count.Should().Be(2);
    }

    [Fact]
    public void TruncateAndFlag_Sets_Truncated_And_Stops_Recording()
    {
        var cap = new CapturingOpLogger();
        var opts = new ItemLogOptions(InitialCapacity: 1, MaxEntries: 1, Overflow: ItemLogOverflowPolicy.TruncateAndFlag, SpillDirectory: Path.GetTempPath());
        var fac = new ItemLogFactory(opts, cap, () => Guid.Empty, () => null);
        using var scope = fac.Start("it2");

        scope.Log(LogSeverity.Info, "c", "s", "m1");
        scope.Log(LogSeverity.Info, "c", "s", "m2"); // should be ignored

        var res = scope.Result;
        res.Truncated.Should().BeTrue();
        res.Count.Should().Be(1);
    }

    [Fact]
    public void SpillToDisk_Writes_File_And_Appends()
    {
        var tmp = Path.Combine(Path.GetTempPath(), "spill-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tmp);
        try
        {
            var cap = new CapturingOpLogger();
            var opts = new ItemLogOptions(InitialCapacity: 2, MaxEntries: 2, Overflow: ItemLogOverflowPolicy.SpillToDisk, SpillDirectory: tmp);
            var fac = new ItemLogFactory(opts, cap, () => Guid.NewGuid(), () => "op");
            using var scope = fac.Start("it3");

            scope.Log(LogSeverity.Info, "c", "s", "m1");
            scope.Log(LogSeverity.Warning, "c", "s", "m2");
            scope.Log(LogSeverity.Error, "c", "s", "m3"); // triggers spill append

            var res = scope.Result;
            res.SpillFilePath.Should().NotBeNull();
            File.Exists(res.SpillFilePath!).Should().BeTrue();
            var lines = File.ReadAllLines(res.SpillFilePath!);
            lines.Length.Should().BeGreaterThanOrEqualTo(2);
            lines.Any(l => l.Contains("\"msg\":\"m1\"", StringComparison.Ordinal)).Should().BeTrue();
            lines.Any(l => l.Contains("\"msg\":\"m3\"", StringComparison.Ordinal)).Should().BeTrue();
        }
        finally
        {
            try { Directory.Delete(tmp, true); } catch { }
        }
    }
}
