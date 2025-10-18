using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FileProcessor.Core.Workspace;
using Microsoft.Data.Sqlite;

namespace FileProcessor.Infrastructure.Workspace;

public sealed class SqliteWorkspaceDb : IWorkspaceDb
{
    private SqliteConnection? _conn;

    public async Task InitializeAsync(string dbPath, CancellationToken ct = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);
        var cs = new SqliteConnectionStringBuilder
        {
            DataSource = dbPath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared
        }.ToString();
        _conn = new SqliteConnection(cs);
        await _conn.OpenAsync(ct);

        using (var pragma = _conn.CreateCommand())
        {
            pragma.CommandText = "PRAGMA journal_mode=WAL; PRAGMA synchronous=NORMAL; PRAGMA foreign_keys=ON;";
            await pragma.ExecuteNonQueryAsync(ct);
        }

        var schemaPath = Path.Combine(AppContext.BaseDirectory, "Workspace", "WorkspaceSchema.sql");
        var sql = await File.ReadAllTextAsync(schemaPath, ct);
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = sql;
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public void Dispose() => _conn?.Dispose();

    private static long NowMs() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    public async Task<long> StartSessionAsync(string? appVersion = null, string? userName = null, string? hostName = null, CancellationToken ct = default)
    {
        using var cmd = _conn!.CreateCommand();
        cmd.CommandText = "INSERT INTO sessions(started_at_ms, app_version, user_name, host_name) VALUES($t,$v,$u,$h); SELECT last_insert_rowid();";
        cmd.Parameters.AddWithValue("$t", NowMs());
        cmd.Parameters.AddWithValue("$v", (object?)appVersion ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$u", (object?)userName ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$h", (object?)hostName ?? DBNull.Value);
        var id = (long)(await cmd.ExecuteScalarAsync(ct))!;
        return id;
    }

    public async Task EndSessionAsync(long sessionId, CancellationToken ct = default)
    {
        using var cmd = _conn!.CreateCommand();
        cmd.CommandText = "UPDATE sessions SET ended_at_ms=$t WHERE id=$id";
        cmd.Parameters.AddWithValue("$t", NowMs());
        cmd.Parameters.AddWithValue("$id", sessionId);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<long> StartRunAsync(long sessionId, string type, string? name = null, string? metadataJson = null, long startedAtMs = 0, CancellationToken ct = default)
    {
        using var cmd = _conn!.CreateCommand();
        cmd.CommandText = "INSERT INTO runs(session_id,type,status,started_at_ms,name,metadata_json) VALUES($s,$ty,'running',$t,$n,$m); SELECT last_insert_rowid();";
        cmd.Parameters.AddWithValue("$s", sessionId);
        cmd.Parameters.AddWithValue("$ty", type);
        cmd.Parameters.AddWithValue("$t", startedAtMs == 0 ? NowMs() : startedAtMs);
        cmd.Parameters.AddWithValue("$n", (object?)name ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$m", (object?)metadataJson ?? DBNull.Value);
        return (long)(await cmd.ExecuteScalarAsync(ct))!;
    }

    public async Task EndRunAsync(long runId, string status, long endedAtMs = 0, CancellationToken ct = default)
    {
        using var cmd = _conn!.CreateCommand();
        cmd.CommandText = "UPDATE runs SET status=$st, ended_at_ms=$t WHERE id=$id";
        cmd.Parameters.AddWithValue("$st", status);
        cmd.Parameters.AddWithValue("$t", endedAtMs == 0 ? NowMs() : endedAtMs);
        cmd.Parameters.AddWithValue("$id", runId);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<long> UpsertItemAsync(long runId, string externalId, string status, int highestSeverity = 2, long startedAtMs = 0, long endedAtMs = 0, string? metricsJson = null, CancellationToken ct = default)
    {
        using var tx = await _conn!.BeginTransactionAsync(ct);
        long id;
        using (var find = _conn.CreateCommand())
        {
            find.Transaction = (SqliteTransaction)tx;
            find.CommandText = "SELECT id FROM items WHERE run_id=$r AND external_id=$e LIMIT 1";
            find.Parameters.AddWithValue("$r", runId);
            find.Parameters.AddWithValue("$e", externalId);
            var res = await find.ExecuteScalarAsync(ct);
            id = res is long l ? l : 0L;
        }
        if (id == 0)
        {
            using var ins = _conn.CreateCommand();
            ins.Transaction = (SqliteTransaction)tx;
            ins.CommandText = "INSERT INTO items(run_id, external_id, status, highest_severity, started_at_ms, ended_at_ms, metrics_json) VALUES($r,$e,$s,$h,$st,$en,$m); SELECT last_insert_rowid();";
            ins.Parameters.AddWithValue("$r", runId);
            ins.Parameters.AddWithValue("$e", externalId);
            ins.Parameters.AddWithValue("$s", status);
            ins.Parameters.AddWithValue("$h", highestSeverity);
            ins.Parameters.AddWithValue("$st", startedAtMs == 0 ? (object)DBNull.Value : startedAtMs);
            ins.Parameters.AddWithValue("$en", endedAtMs == 0 ? (object)DBNull.Value : endedAtMs);
            ins.Parameters.AddWithValue("$m", (object?)metricsJson ?? DBNull.Value);
            id = (long)(await ins.ExecuteScalarAsync(ct))!;
        }
        else
        {
            using var up = _conn.CreateCommand();
            up.Transaction = (SqliteTransaction)tx;
            up.CommandText = "UPDATE items SET status=$s, highest_severity=max(coalesce(highest_severity,2),$h), ended_at_ms=coalesce($en,ended_at_ms) WHERE id=$id";
            up.Parameters.AddWithValue("$s", status);
            up.Parameters.AddWithValue("$h", highestSeverity);
            up.Parameters.AddWithValue("$en", endedAtMs == 0 ? (object)DBNull.Value : endedAtMs);
            up.Parameters.AddWithValue("$id", id);
            await up.ExecuteNonQueryAsync(ct);
        }
        await tx.CommitAsync(ct);
        return id;
    }

    public async Task<long> AppendLogAsync(LogWrite log, CancellationToken ct = default)
    {
        using var cmd = _conn!.CreateCommand();
        cmd.CommandText = @"INSERT INTO log_entries(ts_ms, level, category, subcategory, message, data_json, session_id, run_id, item_id, source)
                             VALUES($ts,$lv,$c,$s,$m,$d,$sid,$rid,$iid,$src); SELECT last_insert_rowid();";
        cmd.Parameters.AddWithValue("$ts", log.TsMs);
        cmd.Parameters.AddWithValue("$lv", log.Level);
        cmd.Parameters.AddWithValue("$c", (object?)log.Category ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$s", (object?)log.Subcategory ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$m", (object?)log.Message ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$d", (object?)log.DataJson ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$sid", (object?)log.SessionId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$rid", (object?)log.RunId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$iid", (object?)log.ItemId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$src", (object?)log.Source ?? DBNull.Value);
        var id = (long)(await cmd.ExecuteScalarAsync(ct))!;
        return id;
    }

    public async Task<IReadOnlyList<LogGroupCount>> QueryGroupCountsAsync(LogQuery q, CancellationToken ct = default)
    {
        var where = BuildWhere(q, out var pars);
        using var cmd = _conn!.CreateCommand();
        cmd.CommandText = $"SELECT category, subcategory, COUNT(*) cnt, MAX(level) mx FROM log_entries {where} GROUP BY category, subcategory ORDER BY category, subcategory";
        foreach (var p in pars) cmd.Parameters.Add(p);
        using var rdr = await cmd.ExecuteReaderAsync(ct);
        var list = new List<LogGroupCount>(256);
        while (await rdr.ReadAsync(ct))
        {
            list.Add(new LogGroupCount(rdr.IsDBNull(0) ? null : rdr.GetString(0), rdr.IsDBNull(1) ? null : rdr.GetString(1), rdr.GetInt32(2), rdr.GetInt32(3)));
        }
        return list;
    }

    public async Task<IReadOnlyList<LogRow>> QueryLogsAsync(LogQuery q, CancellationToken ct = default)
    {
        var where = BuildWhere(q, out var pars);
        using var cmd = _conn!.CreateCommand();
        cmd.CommandText = $"SELECT id, ts_ms, level, category, subcategory, message, data_json, run_id, item_id FROM log_entries {where} ORDER BY ts_ms LIMIT $lim OFFSET $off";
        foreach (var p in pars) cmd.Parameters.Add(p);
        cmd.Parameters.AddWithValue("$lim", q.PageSize);
        cmd.Parameters.AddWithValue("$off", q.Page * q.PageSize);
        using var rdr = await cmd.ExecuteReaderAsync(ct);
        var list = new List<LogRow>(q.PageSize);
        while (await rdr.ReadAsync(ct))
        {
            list.Add(new LogRow(
                rdr.GetInt64(0),
                rdr.GetInt64(1),
                rdr.GetInt32(2),
                rdr.IsDBNull(3) ? null : rdr.GetString(3),
                rdr.IsDBNull(4) ? null : rdr.GetString(4),
                rdr.IsDBNull(5) ? null : rdr.GetString(5),
                rdr.IsDBNull(6) ? null : rdr.GetString(6),
                rdr.IsDBNull(7) ? null : rdr.GetInt64(7),
                rdr.IsDBNull(8) ? null : rdr.GetInt64(8)));
        }
        return list;
    }

    private string BuildWhere(LogQuery q, out List<SqliteParameter> pars)
    {
        var parts = new List<string>();
        pars = new List<SqliteParameter>();
        if (q.RunId is long rid) { parts.Add("run_id=$rid"); pars.Add(new SqliteParameter("$rid", rid)); }
        if (q.ItemId is long iid) { parts.Add("item_id=$iid"); pars.Add(new SqliteParameter("$iid", iid)); }
        if (q.MinLevel is int min) { parts.Add("level >= $min"); pars.Add(new SqliteParameter("$min", min)); }
        if (q.MaxLevel is int max) { parts.Add("level <= $max"); pars.Add(new SqliteParameter("$max", max)); }
        if (!string.IsNullOrWhiteSpace(q.Category)) { parts.Add("category = $cat"); pars.Add(new SqliteParameter("$cat", q.Category)); }
        if (!string.IsNullOrWhiteSpace(q.Subcategory)) { parts.Add("subcategory = $sub"); pars.Add(new SqliteParameter("$sub", q.Subcategory)); }
        if (q.FromTsMs is long from) { parts.Add("ts_ms >= $from"); pars.Add(new SqliteParameter("$from", from)); }
        if (q.ToTsMs is long to) { parts.Add("ts_ms <= $to"); pars.Add(new SqliteParameter("$to", to)); }
        if (!string.IsNullOrWhiteSpace(q.TextContains)) { parts.Add("message LIKE $txt"); pars.Add(new SqliteParameter("$txt", "%" + q.TextContains + "%")); }
        if (q.SessionId is long sid) { parts.Add("session_id=$sid"); pars.Add(new SqliteParameter("$sid", sid)); }
        return parts.Count == 0 ? string.Empty : ("WHERE " + string.Join(" AND ", parts));
    }
}
