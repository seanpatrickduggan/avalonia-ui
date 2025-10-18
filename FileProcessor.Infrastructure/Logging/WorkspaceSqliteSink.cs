using System;
using FileProcessor.Core.Workspace;
using Serilog.Core;
using Serilog.Events;
using System.Text.Json;
using FileProcessor.Infrastructure.Workspace;
using Wdb = FileProcessor.Infrastructure.Workspace.WorkspaceDbService;

namespace FileProcessor.Infrastructure.Logging;

// Serilog sink that mirrors log events into the workspace SQLite DB via WorkspaceDbService.
public sealed class WorkspaceSqliteSink : ILogEventSink
{
    public void Emit(LogEvent e)
    {
        try
        {
            var tsMs = e.Timestamp.ToUniversalTime().ToUnixTimeMilliseconds();
            int level = e.Level switch
            {
                LogEventLevel.Verbose => 0,
                LogEventLevel.Debug => 1,
                LogEventLevel.Information => 2,
                LogEventLevel.Warning => 3,
                LogEventLevel.Error => 4,
                LogEventLevel.Fatal => 5,
                _ => 2
            };

            e.Properties.TryGetValue("cat", out var cat);
            e.Properties.TryGetValue("sub", out var sub);
            e.Properties.TryGetValue("item", out var item);
            e.Properties.TryGetValue("source", out var source);
            e.Properties.TryGetValue("Data", out var data);

            string message = e.RenderMessage(null);
            string? dataJson = data != null ? JsonSerializer.Serialize(data) : null;

            long? itemId = null;
            if (item is ScalarValue sv)
            {
                if (sv.Value is long l) itemId = l;
                else if (sv.Value is int i) itemId = i;
                else if (sv.Value is string s && long.TryParse(s, out var pl)) itemId = pl;
            }

            var write = new LogWrite(
                TsMs: tsMs,
                Level: level,
                Category: cat?.ToString().Trim('"'),
                Subcategory: sub?.ToString().Trim('"'),
                Message: message,
                DataJson: dataJson,
                SessionId: Wdb.SessionIdOrNull,
                RunId: Wdb.CurrentRunId == 0 ? (long?)null : Wdb.CurrentRunId,
                ItemId: itemId,
                Source: source?.ToString().Trim('"') ?? "serilog"
            );

            Wdb.AppendOrBuffer(write);
        }
        catch
        {
            // never throw from logging
        }
    }
}
