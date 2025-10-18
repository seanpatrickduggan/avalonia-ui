using System;
using System.Text.Json;
using FileProcessor.Core.Logging;
using FileProcessor.Core.Workspace;
using Serilog;
using Serilog.Events;
using FileProcessor.Infrastructure.Workspace;

namespace FileProcessor.Infrastructure.Logging;

// Logs to Serilog and mirrors lightweight rows into the workspace DB.
public sealed class WorkspaceRunStructuredLogger : IRunStructuredLogger
{
    private readonly ILogger _logger;

    public WorkspaceRunStructuredLogger(ILogger logger)
    {
        _logger = logger;
    }

    public void Log(Guid runId, string? batchType, string itemId, LogSeverity level, string category, string subcategory, string message, object? data = null)
    {
        // 1) Serilog event
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

        // 2) Append to workspace DB (best-effort, fire-and-forget)
        _ = AppendToDbAsync(level, category, subcategory, message, data);
    }

    private static async System.Threading.Tasks.Task AppendToDbAsync(LogSeverity level, string category, string subcategory, string message, object? data)
    {
        try
        {
            var tsMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var dataJson = data is null ? null : JsonSerializer.Serialize(data);
            var runId = WorkspaceDbService.CurrentRunId == 0 ? (long?)null : WorkspaceDbService.CurrentRunId;
            var write = new LogWrite(tsMs, (int)level, category, subcategory, message, dataJson, WorkspaceDbService.SessionId, runId, null, "ui");
            await WorkspaceDbService.Db.AppendLogAsync(write);
        }
        catch { /* ignore logging failures */ }
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
