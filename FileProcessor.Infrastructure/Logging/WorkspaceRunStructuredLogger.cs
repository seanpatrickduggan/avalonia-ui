using System;
using FileProcessor.Core.Logging;
using Serilog;
using Serilog.Events;

namespace FileProcessor.Infrastructure.Logging;

// Logs to Serilog; DB mirroring is handled by the WorkspaceSqliteSink.
public sealed class WorkspaceRunStructuredLogger : IRunStructuredLogger
{
    private readonly ILogger _logger;

    public WorkspaceRunStructuredLogger(ILogger logger)
    {
        _logger = logger;
    }

    public void Log(Guid runId, string? batchType, string itemId, LogSeverity level, string category, string subcategory, string message, object? data = null)
    {
        var evtLevel = MapLevel(level);
        var lg = _logger
            .ForContext("run", runId)
            .ForContext("batch", batchType)
            .ForContext("item", itemId)
            .ForContext("cat", category)
            .ForContext("sub", subcategory)
            .ForContext("severityRank", (int)level);
        if (data is null) lg.Write(evtLevel, message);
        else lg.Write(evtLevel, "{Message} {@Data}", message, data);
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
