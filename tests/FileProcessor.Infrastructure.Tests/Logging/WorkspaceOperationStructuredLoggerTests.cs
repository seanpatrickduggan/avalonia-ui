using FileProcessor.Core.Logging;
using FileProcessor.Infrastructure.Logging;

using FluentAssertions;

using Serilog;
using Serilog.Core;
using Serilog.Events;

using Xunit;

namespace FileProcessor.Infrastructure.Tests.Logging;

public class WorkspaceOperationStructuredLoggerTests
{
    [Fact]
    public void Log_WithoutData_WritesMessageWithContexts()
    {
        var testSink = new TestSink();
        var logger = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .WriteTo.Sink(testSink)
            .CreateLogger();

        var structuredLogger = new WorkspaceOperationStructuredLogger(logger);
        var operationId = Guid.NewGuid();
        var operationType = "TestOp";
        var itemId = "item1";
        var level = LogSeverity.Info;
        var category = "TestCat";
        var subcategory = "TestSub";
        var message = "Test message";

        structuredLogger.Log(operationId, operationType, itemId, level, category, subcategory, message);

        testSink.Events.Should().HaveCount(1);
        var logEvent = testSink.Events[0];
        logEvent.Level.Should().Be(LogEventLevel.Information);
        logEvent.MessageTemplate.Text.Should().Be(message);
        logEvent.Properties.Should().ContainKey("operation");
        ((ScalarValue)logEvent.Properties["operation"]).Value.Should().Be(operationId);
        ((ScalarValue)logEvent.Properties["operation_type"]).Value.Should().Be(operationType);
        ((ScalarValue)logEvent.Properties["item"]).Value.Should().Be(itemId);
        ((ScalarValue)logEvent.Properties["cat"]).Value.Should().Be(category);
        ((ScalarValue)logEvent.Properties["sub"]).Value.Should().Be(subcategory);
        ((ScalarValue)logEvent.Properties["severityRank"]).Value.Should().Be((int)level);
    }

    [Fact]
    public void Log_WithData_WritesMessageWithData()
    {
        var testSink = new TestSink();
        var logger = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .WriteTo.Sink(testSink)
            .CreateLogger();

        var structuredLogger = new WorkspaceOperationStructuredLogger(logger);
        var operationId = Guid.NewGuid();
        var data = new { Key = "Value" };

        structuredLogger.Log(operationId, null, "item", LogSeverity.Warning, "cat", "sub", "Message", data);

        testSink.Events.Should().HaveCount(1);
        var logEvent = testSink.Events[0];
        logEvent.Level.Should().Be(LogEventLevel.Warning);
        logEvent.MessageTemplate.Text.Should().Be("{Message} {@Data}");
        ((ScalarValue)logEvent.Properties["Message"]).Value.Should().Be("Message");
        logEvent.Properties.Should().ContainKey("Data");
    }

    [Theory]
    [InlineData(LogSeverity.Trace, LogEventLevel.Verbose)]
    [InlineData(LogSeverity.Debug, LogEventLevel.Debug)]
    [InlineData(LogSeverity.Info, LogEventLevel.Information)]
    [InlineData(LogSeverity.Warning, LogEventLevel.Warning)]
    [InlineData(LogSeverity.Error, LogEventLevel.Error)]
    [InlineData(LogSeverity.Critical, LogEventLevel.Fatal)]
    public void Log_MapsLevelsCorrectly(LogSeverity inputLevel, LogEventLevel expectedEventLevel)
    {
        var testSink = new TestSink();
        var logger = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .WriteTo.Sink(testSink)
            .CreateLogger();

        var structuredLogger = new WorkspaceOperationStructuredLogger(logger);
        structuredLogger.Log(Guid.NewGuid(), "type", "item", inputLevel, "cat", "sub", "msg");

        testSink.Events.Should().HaveCount(1);
        testSink.Events[0].Level.Should().Be(expectedEventLevel);
    }

    private class TestSink : ILogEventSink
    {
        public List<LogEvent> Events { get; } = new();

        public void Emit(LogEvent logEvent)
        {
            Events.Add(logEvent);
        }
    }
}
