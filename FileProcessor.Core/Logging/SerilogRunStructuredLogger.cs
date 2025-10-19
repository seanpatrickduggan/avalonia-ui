using System;
using Serilog;
using Serilog.Events;

namespace FileProcessor.Core.Logging;

// Obsolete: was run-based; now operation-based Serilog adapter kept in Core for convenience.
public sealed class SerilogOperationStructuredLogger : IOperationStructuredLogger
{
    private readonly ILogger _logger;

    public SerilogOperationStructuredLogger(ILogger logger)
    {
        _logger = logger;
    }

    public void Log(Guid operationId, string? operationType, string itemId, LogSeverity level, string category, string subcategory, string message, object? data = null)
    {
        var evtLevel = MapLevel(level);
        var lg = _logger
            .ForContext("operation", operationId)
            .ForContext("operation_type", operationType)
            .ForContext("item", itemId)
            .ForContext("cat", category)
            .ForContext("sub", subcategory)
            .ForContext("severityRank", (int)level);

        if (data is null)
        {
            lg.Write(evtLevel, message);
        }
        else
        {
            lg.Write(evtLevel, "{Message} {@Data}", message, data);
        }
    }

    private static LogEventLevel MapLevel(LogSeverity level) => level switch
    {
        LogSeverity.Trace => LogEventLevel.Verbose,
        LogSeverity.Debug => LogEventLevel.Debug,
        LogSeverity.Info => LogEventLevel.Information,
        LogSeverity.Warning => LogEventLevel.Warning,
        LogSeverity.Error => LogEventLevel.Error,
        LogSeverity.Critical => LogEventLevel.Fatal,
        _ => LogEventLevel.Information
    };
}
