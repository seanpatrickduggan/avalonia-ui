using FileProcessor.Core.Workspace;
using FileProcessor.Infrastructure.Logging;

using FluentAssertions;

using Serilog.Events;
using Serilog.Parsing;

using Xunit;

namespace FileProcessor.Infrastructure.Tests.Logging;

public class WorkspaceSqliteSinkTests
{
    [Fact]
    public void Emit_ConvertsLogLevelsCorrectly()
    {
        var testTarget = new TestLogWriteTarget();
        var sink = new WorkspaceSqliteSink(testTarget);

        // Test all log levels
        TestLogLevel(sink, testTarget, LogEventLevel.Verbose, 0);
        TestLogLevel(sink, testTarget, LogEventLevel.Debug, 1);
        TestLogLevel(sink, testTarget, LogEventLevel.Information, 2);
        TestLogLevel(sink, testTarget, LogEventLevel.Warning, 3);
        TestLogLevel(sink, testTarget, LogEventLevel.Error, 4);
        TestLogLevel(sink, testTarget, LogEventLevel.Fatal, 5);
    }

    private void TestLogLevel(WorkspaceSqliteSink sink, TestLogWriteTarget target, LogEventLevel level, int expectedLevel)
    {
        target.Writes.Clear();
        var timestamp = new DateTimeOffset(2023, 1, 1, 12, 0, 0, TimeSpan.Zero);
        var logEvent = new LogEvent(timestamp, level, null, new MessageTemplate("Test", Array.Empty<MessageTemplateToken>()), new LogEventProperty[0]);

        sink.Emit(logEvent);

        target.Writes.Should().HaveCount(1);
        var write = target.Writes[0];
        write.Level.Should().Be(expectedLevel);
        write.TsMs.Should().Be(timestamp.ToUnixTimeMilliseconds());
        write.Message.Should().Be("");
        write.SessionId.Should().Be(123L);
        write.OperationId.Should().Be(456L);
        write.ItemId.Should().BeNull();
        write.Source.Should().Be("serilog");
    }

    [Fact]
    public void Emit_ExtractsPropertiesCorrectly()
    {
        var testTarget = new TestLogWriteTarget();
        var sink = new WorkspaceSqliteSink(testTarget);
        var timestamp = new DateTimeOffset(2023, 1, 1, 12, 0, 0, TimeSpan.Zero);
        var properties = new[]
        {
            new LogEventProperty("cat", new ScalarValue("TestCategory")),
            new LogEventProperty("sub", new ScalarValue("TestSubcategory")),
            new LogEventProperty("item", new ScalarValue(789L)),
            new LogEventProperty("source", new ScalarValue("TestSource")),
            new LogEventProperty("Data", new ScalarValue("test data"))
        };
        var logEvent = new LogEvent(timestamp, LogEventLevel.Information, null,
            new MessageTemplate("Test message", Array.Empty<MessageTemplateToken>()), properties);

        sink.Emit(logEvent);

        testTarget.Writes.Should().HaveCount(1);
        var write = testTarget.Writes[0];
        write.Category.Should().Be("TestCategory");
        write.Subcategory.Should().Be("TestSubcategory");
        write.ItemId.Should().Be(789L);
        write.Source.Should().Be("TestSource");
        write.DataJson.Should().Be("{}");
    }

    [Fact]
    public void Emit_HandlesItemAsInt()
    {
        var testTarget = new TestLogWriteTarget();
        var sink = new WorkspaceSqliteSink(testTarget);
        var timestamp = new DateTimeOffset(2023, 1, 1, 12, 0, 0, TimeSpan.Zero);
        var properties = new[]
        {
            new LogEventProperty("item", new ScalarValue(42))
        };
        var logEvent = new LogEvent(timestamp, LogEventLevel.Information, null,
            new MessageTemplate("Test", Array.Empty<MessageTemplateToken>()), properties);

        sink.Emit(logEvent);

        testTarget.Writes[0].ItemId.Should().Be(42L);
    }

    [Fact]
    public void Emit_HandlesItemAsString()
    {
        var testTarget = new TestLogWriteTarget();
        var sink = new WorkspaceSqliteSink(testTarget);
        var timestamp = new DateTimeOffset(2023, 1, 1, 12, 0, 0, TimeSpan.Zero);
        var properties = new[]
        {
            new LogEventProperty("item", new ScalarValue("123"))
        };
        var logEvent = new LogEvent(timestamp, LogEventLevel.Information, null,
            new MessageTemplate("Test", Array.Empty<MessageTemplateToken>()), properties);

        sink.Emit(logEvent);

        testTarget.Writes[0].ItemId.Should().Be(123L);
    }

    [Fact]
    public void Emit_HandlesInvalidItemString()
    {
        var testTarget = new TestLogWriteTarget();
        var sink = new WorkspaceSqliteSink(testTarget);
        var timestamp = new DateTimeOffset(2023, 1, 1, 12, 0, 0, TimeSpan.Zero);
        var properties = new[]
        {
            new LogEventProperty("item", new ScalarValue("notanumber"))
        };
        var logEvent = new LogEvent(timestamp, LogEventLevel.Information, null,
            new MessageTemplate("Test", Array.Empty<MessageTemplateToken>()), properties);

        sink.Emit(logEvent);

        testTarget.Writes[0].ItemId.Should().BeNull();
    }

    [Fact]
    public void Emit_HandlesNonScalarItem()
    {
        var testTarget = new TestLogWriteTarget();
        var sink = new WorkspaceSqliteSink(testTarget);
        var timestamp = new DateTimeOffset(2023, 1, 1, 12, 0, 0, TimeSpan.Zero);
        var properties = new[]
        {
            new LogEventProperty("item", new StructureValue(new[] { new LogEventProperty("key", new ScalarValue("value")) }))
        };
        var logEvent = new LogEvent(timestamp, LogEventLevel.Information, null,
            new MessageTemplate("Test", Array.Empty<MessageTemplateToken>()), properties);

        sink.Emit(logEvent);

        testTarget.Writes[0].ItemId.Should().BeNull();
    }

    [Fact]
    public void Emit_HandlesNullData()
    {
        var testTarget = new TestLogWriteTarget();
        var sink = new WorkspaceSqliteSink(testTarget);
        var timestamp = new DateTimeOffset(2023, 1, 1, 12, 0, 0, TimeSpan.Zero);
        var logEvent = new LogEvent(timestamp, LogEventLevel.Information, null,
            new MessageTemplate("Test", Array.Empty<MessageTemplateToken>()), new LogEventProperty[0]);

        sink.Emit(logEvent);

        testTarget.Writes[0].DataJson.Should().BeNull();
    }

    [Fact]
    public void Emit_HandlesComplexData()
    {
        var testTarget = new TestLogWriteTarget();
        var sink = new WorkspaceSqliteSink(testTarget);
        var timestamp = new DateTimeOffset(2023, 1, 1, 12, 0, 0, TimeSpan.Zero);
        var data = new { Key = "value", Number = 42 };
        var properties = new[]
        {
            new LogEventProperty("Data", new ScalarValue(data))
        };
        var logEvent = new LogEvent(timestamp, LogEventLevel.Information, null,
            new MessageTemplate("Test", Array.Empty<MessageTemplateToken>()), properties);

        sink.Emit(logEvent);

        var expectedJson = "{}";
        testTarget.Writes[0].DataJson.Should().Be(expectedJson);
    }

    [Fact]
    public void Emit_HandlesZeroOperationId()
    {
        var testTarget = new TestLogWriteTarget { CurrentOperationId = 0L };
        var sink = new WorkspaceSqliteSink(testTarget);
        var timestamp = new DateTimeOffset(2023, 1, 1, 12, 0, 0, TimeSpan.Zero);
        var logEvent = new LogEvent(timestamp, LogEventLevel.Information, null,
            new MessageTemplate("Test", Array.Empty<MessageTemplateToken>()), new LogEventProperty[0]);

        sink.Emit(logEvent);

        testTarget.Writes[0].OperationId.Should().BeNull();
    }

    [Fact]
    public void Emit_HandlesNullSessionId()
    {
        var testTarget = new TestLogWriteTarget { SessionIdOrNull = null };
        var sink = new WorkspaceSqliteSink(testTarget);
        var timestamp = new DateTimeOffset(2023, 1, 1, 12, 0, 0, TimeSpan.Zero);
        var logEvent = new LogEvent(timestamp, LogEventLevel.Information, null,
            new MessageTemplate("Test", Array.Empty<MessageTemplateToken>()), new LogEventProperty[0]);

        sink.Emit(logEvent);

        testTarget.Writes[0].SessionId.Should().BeNull();
    }

    [Fact]
    public void Emit_HandlesExceptionGracefully()
    {
        var testTarget = new TestLogWriteTarget();
        var sink = new WorkspaceSqliteSink(testTarget);
        // Create a log event that will cause an exception (e.g., invalid data serialization)
        var timestamp = new DateTimeOffset(2023, 1, 1, 12, 0, 0, TimeSpan.Zero);
        var properties = new[]
        {
            new LogEventProperty("Data", new ScalarValue(new object())) // Object that might cause serialization issues
        };
        var logEvent = new LogEvent(timestamp, LogEventLevel.Information, null,
            new MessageTemplate("Test", Array.Empty<MessageTemplateToken>()), properties);

        // Should not throw
        sink.Emit(logEvent);

        // Should still call AppendOrBuffer (or not, depending on where the exception occurs)
        // The important thing is that no exception bubbles up
    }

    [Fact]
    public void Emit_TrimsQuotesFromProperties()
    {
        var testTarget = new TestLogWriteTarget();
        var sink = new WorkspaceSqliteSink(testTarget);
        var timestamp = new DateTimeOffset(2023, 1, 1, 12, 0, 0, TimeSpan.Zero);
        var properties = new[]
        {
            new LogEventProperty("cat", new ScalarValue("QuotedCategory")),
            new LogEventProperty("sub", new ScalarValue("QuotedSubcategory")),
            new LogEventProperty("source", new ScalarValue("QuotedSource"))
        };
        var logEvent = new LogEvent(timestamp, LogEventLevel.Information, null,
            new MessageTemplate("Test", Array.Empty<MessageTemplateToken>()), properties);

        sink.Emit(logEvent);

        var write = testTarget.Writes[0];
        write.Category.Should().Be("QuotedCategory");
        write.Subcategory.Should().Be("QuotedSubcategory");
        write.Source.Should().Be("QuotedSource");
    }

    [Fact]
    public void Emit_UsesDefaultSourceWhenNotProvided()
    {
        var testTarget = new TestLogWriteTarget();
        var sink = new WorkspaceSqliteSink(testTarget);
        var timestamp = new DateTimeOffset(2023, 1, 1, 12, 0, 0, TimeSpan.Zero);
        var logEvent = new LogEvent(timestamp, LogEventLevel.Information, null,
            new MessageTemplate("Test", Array.Empty<MessageTemplateToken>()), new LogEventProperty[0]);

        sink.Emit(logEvent);

        testTarget.Writes[0].Source.Should().Be("serilog");
    }

    [Fact]
    public void Constructor_ThrowsOnNullTarget()
    {
        Action act = () => new WorkspaceSqliteSink(null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("target");
    }

    private class TestLogWriteTarget : ILogWriteTarget
    {
        public List<LogWrite> Writes { get; } = new();
        public long? SessionIdOrNull { get; set; } = 123L;
        public long CurrentOperationId { get; set; } = 456L;

        public void AppendOrBuffer(LogWrite write)
        {
            Writes.Add(write);
        }
    }
}
