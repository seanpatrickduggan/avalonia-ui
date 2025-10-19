using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FileProcessor.Core.Workspace;

namespace FileProcessor.Infrastructure.Diagnostics;

/// <summary>
/// Lightweight integration probe to verify that a single log event results in exactly one row in the DB.
/// Use during development/tests to guard against duplicate sink instances or double writes.
/// </summary>
public static class IntegrationProbe
{
    /// <summary>
    /// Emits a unique probe log via the runtime writer and ensures exactly one DB row is present.
    /// Returns (ok, count) where ok is true when count == 1.
    /// </summary>
    public static async Task<(bool ok, int count, string marker)> VerifyOneEventOneRowAsync(
        IWorkspaceRuntime runtime,
        IWorkspaceDb db,
        TimeProvider time,
        CancellationToken ct = default)
    {
        // Ensure runtime initialized
        await runtime.InitializeAsync(ct);

        var marker = $"probe:{Guid.NewGuid():N}";
        var write = new LogWrite(
            TsMs: time.GetUtcNow().ToUnixTimeMilliseconds(),
            Level: 2,
            Category: "probe",
            Subcategory: "one-event-one-row",
            Message: marker,
            DataJson: null,
            SessionId: runtime.SessionId,
            OperationId: runtime.CurrentOperationId == 0 ? null : runtime.CurrentOperationId,
            ItemId: null,
            Source: "integration-probe");

        await runtime.AppendOrBufferAsync(write, ct);
        await runtime.FlushAsync(ct);

        var rows = await db.QueryLogsAsync(new LogQuery(
            SessionId: runtime.SessionId,
            Category: "probe",
            Subcategory: "one-event-one-row",
            TextContains: marker,
            Page: 0,
            PageSize: 100
        ), ct);

        var count = rows.Count(r => string.Equals(r.Message, marker, StringComparison.Ordinal));
        return (count == 1, count, marker);
    }
}
