using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Collections.Generic;
using FileProcessor.Core.Logging;
using FluentAssertions;
using Xunit;

namespace FileProcessor.Core.Tests;

public class ItemLogExtensionsTests
{
    private sealed class FakeScope : IItemLogScope
    {
        public record Rec(LogSeverity Level, string Category, string Subcategory, string Message, object? Data);
        public List<Rec> Recs { get; } = new();
        public string ItemId { get; }
        public ItemLogResult Result => ItemLogResult.Empty(ItemId);
        public FakeScope(string itemId) { ItemId = itemId; }
        public void Log(LogSeverity level, string category, string subcategory, string message, object? data = null)
            => Recs.Add(new Rec(level, category, subcategory, message, data));
        public void Dispose() { }
    }

    [Fact]
    public void Defaults_Category_And_Subcategory()
    {
        var scope = new FakeScope("it-001");
        scope.Info("hello");
        scope.Recs.Should().HaveCount(1);
        var r = scope.Recs.First();
        r.Level.Should().Be(LogSeverity.Info);
        r.Category.Should().Be("process");
        r.Subcategory.Should().Be("it-001");
        r.Message.Should().Be("hello");
        r.Data.Should().BeNull();
    }

    [Fact]
    public void Respects_Provided_Category_And_Subcategory()
    {
        var scope = new FakeScope("it-002");
        scope.Error("oops", category: "cat", subcategory: "sub");
        var r = scope.Recs.Single();
        r.Level.Should().Be(LogSeverity.Error);
        r.Category.Should().Be("cat");
        r.Subcategory.Should().Be("sub");
    }

    [Fact]
    public void Warn_And_Warning_Are_Equivalent()
    {
        var scope = new FakeScope("it-003");
        scope.Warn("w1");
        scope.Warning("w2");
        scope.Recs.Should().HaveCount(2);
        scope.Recs.All(x => x.Level == LogSeverity.Warning).Should().BeTrue();
    }

    [Fact]
    public void TimedT_Success_Logs_Debug_With_Duration()
    {
        var scope = new FakeScope("it-004");
        var res = scope.Timed("op", () => 123);
        res.Should().Be(123);
        var r = scope.Recs.Last();
        r.Level.Should().Be(LogSeverity.Debug);
        r.Message.Should().Contain("completed");
        r.Category.Should().Be("process");
        r.Subcategory.Should().Be("it-004");
        r.Data.Should().NotBeNull();
        JsonSerializer.Serialize(r.Data).Should().Contain("durationMs");
    }

    [Fact]
    public void TimedT_Failure_Logs_Error_Then_Rethrows()
    {
        var scope = new FakeScope("it-005");
        Action act = () => scope.Timed<int>("op2", () => throw new InvalidOperationException("bad"));
        act.Should().Throw<InvalidOperationException>().WithMessage("bad");
        var r = scope.Recs.Last();
        r.Level.Should().Be(LogSeverity.Error);
        r.Message.Should().Contain("failed: bad");
        JsonSerializer.Serialize(r.Data).Should().Contain("durationMs");
        JsonSerializer.Serialize(r.Data).Should().Contain("\"Message\":\"bad\"");
    }

    [Fact]
    public void Timed_Void_Success_And_Failure()
    {
        var scope = new FakeScope("it-006");
        scope.Timed("op3", () => { /* ok */ });
        var r1 = scope.Recs.Last();
        r1.Level.Should().Be(LogSeverity.Debug);
        r1.Message.Should().Contain("completed");

        Action act = () => scope.Timed("op4", () => throw new Exception("boom"));
        act.Should().Throw<Exception>().WithMessage("boom");
        var r2 = scope.Recs.Last();
        r2.Level.Should().Be(LogSeverity.Error);
        r2.Message.Should().Contain("failed: boom");
    }

    [Fact]
    public void Other_Severities_Are_Wired()
    {
        var scope = new FakeScope("it-007");
        scope.Trace("t");
        scope.Debug("d");
        scope.Critical("c");
        scope.Recs.Select(x => x.Level).Should().Contain(new[] { LogSeverity.Trace, LogSeverity.Debug, LogSeverity.Critical });
    }
}
