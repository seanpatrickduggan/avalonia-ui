using System;
using FileProcessor.Core.Workspace;
using Serilog.Core;
using Serilog.Events;
using System.Text.Json;
using Serilog.Debugging; // debug-only diagnostics
using System.Threading; // Interlocked

namespace FileProcessor.Infrastructure.Logging;

public interface ILogWriteTarget
{
    long? SessionIdOrNull { get; }
    long CurrentOperationId { get; }
    void AppendOrBuffer(FileProcessor.Core.Workspace.LogWrite write);
}

// Serilog sink that mirrors log events into the workspace via an injected target.
public sealed class WorkspaceSqliteSink : ILogEventSink
{
    private readonly ILogWriteTarget _target;
#if DEBUG
    private static int s_instanceCount;
#endif

    public WorkspaceSqliteSink(ILogWriteTarget target)
    {
        _target = target ?? throw new ArgumentNullException(nameof(target));
#if DEBUG
        var count = Interlocked.Increment(ref s_instanceCount);
        if (count > 1)
        {
            SelfLog.WriteLine("[WorkspaceSqliteSink] Multiple instances created in process: {0}. Ensure only one sink is added to the pipeline.", count);
        }
#endif
    }

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
                SessionId: _target.SessionIdOrNull,
                OperationId: _target.CurrentOperationId == 0 ? (long?)null : _target.CurrentOperationId,
                ItemId: itemId,
                Source: source?.ToString().Trim('"') ?? "serilog"
            );

            _target.AppendOrBuffer(write);
        }
        catch
        {
            // never throw from logging
        }
    }
}
