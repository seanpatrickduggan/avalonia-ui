using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FileProcessor.Core.Logging;

namespace FileProcessor.Core.Workspace;

// Core contracts for a workspace database used by the app and viewers.
// Keep this project free of concrete DB dependencies.

public interface IWorkspaceDb : IRunStore, ILogStore, IDisposable
{
    Task InitializeAsync(string dbPath, CancellationToken ct = default);
    Task<long> StartSessionAsync(string? appVersion = null, string? userName = null, string? hostName = null, CancellationToken ct = default);
    Task EndSessionAsync(long sessionId, CancellationToken ct = default);
}

public interface IRunStore
{
    Task<long> StartRunAsync(long sessionId, string type, string? name = null, string? metadataJson = null, long startedAtMs = 0, CancellationToken ct = default);
    Task EndRunAsync(long runId, string status, long endedAtMs = 0, CancellationToken ct = default);
    Task<long> UpsertItemAsync(long runId, string externalId, string status, int highestSeverity = (int)LogSeverity.Info, long startedAtMs = 0, long endedAtMs = 0, string? metricsJson = null, CancellationToken ct = default);
}

public interface ILogStore
{
    Task<long> AppendLogAsync(LogWrite log, CancellationToken ct = default);
    Task<IReadOnlyList<LogRow>> QueryLogsAsync(LogQuery q, CancellationToken ct = default);
    Task<IReadOnlyList<LogGroupCount>> QueryGroupCountsAsync(LogQuery q, CancellationToken ct = default);
}

public sealed record LogWrite(
    long TsMs,
    int Level,
    string? Category,
    string? Subcategory,
    string? Message,
    string? DataJson,
    long? SessionId,
    long? RunId,
    long? ItemId,
    string? Source);

public sealed record LogRow(
    long Id,
    long TsMs,
    int Level,
    string? Category,
    string? Subcategory,
    string? Message,
    string? DataJson,
    long? RunId,
    long? ItemId);

public sealed record LogGroupCount(
    string? Category,
    string? Subcategory,
    int Count,
    int MaxLevel);

public sealed record LogQuery(
    long? RunId = null,
    long? ItemId = null,
    int? MinLevel = null,
    int? MaxLevel = null,
    string? Category = null,
    string? Subcategory = null,
    string? TextContains = null,
    int Page = 0,
    int PageSize = 500,
    long? FromTsMs = null,
    long? ToTsMs = null,
    long? SessionId = null);
