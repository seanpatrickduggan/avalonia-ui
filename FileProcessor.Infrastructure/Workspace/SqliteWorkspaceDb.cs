using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FileProcessor.Core.Workspace;
using Microsoft.Data.Sqlite;
using Serilog; // added for user notifications

namespace FileProcessor.Infrastructure.Workspace;

public sealed class SqliteWorkspaceDb : IWorkspaceDb
{
    private const int CurrentSchemaVersion = 2; // bump when schema changes
    private SqliteConnection? _conn;
    private readonly FileProcessor.Core.Abstractions.IFileSystem _fs;
    private readonly TimeProvider _time;

    public SqliteWorkspaceDb(FileProcessor.Core.Abstractions.IFileSystem fs, TimeProvider time)
    {
        _fs = fs;
        _time = time;
    }

    public async Task InitializeAsync(string dbPath, CancellationToken ct = default)
    {
        _fs.CreateDirectory(Path.GetDirectoryName(dbPath)!);
        var baseFileExisted = _fs.FileExists(dbPath);
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
            pragma.CommandText = "PRAGMA journal_mode=WAL; PRAGMA synchronous=NORMAL; PRAGMA foreign_keys=ON; PRAGMA busy_timeout=5000;";
            await pragma.ExecuteNonQueryAsync(ct);
        }

        // Detect existing schema version (if any)
        int existingVer = -1;
        bool hasSchemaInfo = false;
        try
        {
            using var chkTable = _conn.CreateCommand();
            chkTable.CommandText = "SELECT 1 FROM sqlite_master WHERE type='table' AND name='schema_info'";
            var existsObj = await chkTable.ExecuteScalarAsync(ct);
            hasSchemaInfo = existsObj != null;
            if (hasSchemaInfo)
            {
                using var chk = _conn.CreateCommand();
                chk.CommandText = "SELECT version FROM schema_info LIMIT 1";
                var obj = await chk.ExecuteScalarAsync(ct);
                if (obj is long l) existingVer = (int)l;
            }
        }
        catch { hasSchemaInfo = false; existingVer = -1; }

        var infraBaseDir = Path.GetDirectoryName(typeof(SqliteWorkspaceDb).Assembly.Location)!;

        // First-time creation: no base file or no schema_info table
        if (!baseFileExisted || !hasSchemaInfo)
        {
            var schemaPathNew = Path.Combine(infraBaseDir, "Workspace", "WorkspaceSchema.sql");
            var sqlNew = await _fs.ReadAllTextAsync(schemaPathNew, ct);
            using (var cmdNew = _conn.CreateCommand())
            {
                cmdNew.CommandText = sqlNew;
                await cmdNew.ExecuteNonQueryAsync(ct);
            }

            using (var verCmd = _conn.CreateCommand())
            {
                verCmd.CommandText = "CREATE TABLE IF NOT EXISTS schema_info(version INTEGER NOT NULL); DELETE FROM schema_info; INSERT INTO schema_info(version) VALUES($v);";
                verCmd.Parameters.AddWithValue("$v", CurrentSchemaVersion);
                await verCmd.ExecuteNonQueryAsync(ct);
            }

            try
            {
                using var ckp = _conn.CreateCommand();
                ckp.CommandText = "PRAGMA wal_checkpoint(TRUNCATE);";
                await ckp.ExecuteNonQueryAsync(ct);
            }
            catch { }

            Log.Information("Workspace database created with schema v{Version} at {Path}", CurrentSchemaVersion, dbPath);
            return;
        }

        // True mismatch: existing DB with old schema_info version
        if (existingVer != CurrentSchemaVersion)
        {
            try { await _conn.CloseAsync(); } catch { }
            _conn.Dispose();
            try { if (_fs.FileExists(dbPath)) _fs.DeleteFile(dbPath); } catch { /* ignore */ }

            _conn = new SqliteConnection(cs);
            await _conn.OpenAsync(ct);
            using (var pragma2 = _conn.CreateCommand())
            {
                pragma2.CommandText = "PRAGMA journal_mode=WAL; PRAGMA synchronous=NORMAL; PRAGMA foreign_keys=ON; PRAGMA busy_timeout=5000;";
                await pragma2.ExecuteNonQueryAsync(ct);
            }

            var schemaPathNew = Path.Combine(infraBaseDir, "Workspace", "WorkspaceSchema.sql");
            var sqlNew = await _fs.ReadAllTextAsync(schemaPathNew, ct);
            using (var cmdNew = _conn.CreateCommand())
            {
                cmdNew.CommandText = sqlNew;
                await cmdNew.ExecuteNonQueryAsync(ct);
            }

            using (var verCmd = _conn.CreateCommand())
            {
                verCmd.CommandText = "CREATE TABLE IF NOT EXISTS schema_info(version INTEGER NOT NULL); DELETE FROM schema_info; INSERT INTO schema_info(version) VALUES($v);";
                verCmd.Parameters.AddWithValue("$v", CurrentSchemaVersion);
                await verCmd.ExecuteNonQueryAsync(ct);
            }

            try
            {
                using var ckp = _conn.CreateCommand();
                ckp.CommandText = "PRAGMA wal_checkpoint(TRUNCATE);";
                await ckp.ExecuteNonQueryAsync(ct);
            }
            catch { }

            var noticePath = Path.Combine(Path.GetDirectoryName(dbPath)!, "workspace-schema-reset.txt");
            var tsIso = _time.GetUtcNow().ToString("o");
            var msg = $"Workspace database was recreated due to schema version mismatch (found {existingVer}, expected {CurrentSchemaVersion}). A fresh schema has been applied.";
            try { await _fs.WriteAllTextAsync(noticePath, $"{tsIso} {msg}\nDB Path: {dbPath}\n", ct); } catch { }
            Log.Warning(msg);
            return;
        }

        // Existing and up-to-date: ensure schema elements exist
        using (var ensure = _conn.CreateCommand())
        {
            ensure.CommandText = "CREATE TABLE IF NOT EXISTS schema_info(version INTEGER NOT NULL); DELETE FROM schema_info; INSERT INTO schema_info(version) VALUES($v);";
            ensure.Parameters.AddWithValue("$v", CurrentSchemaVersion);
            await ensure.ExecuteNonQueryAsync(ct);
        }

        var schemaPath = Path.Combine(infraBaseDir, "Workspace", "WorkspaceSchema.sql");
        var sql = await _fs.ReadAllTextAsync(schemaPath, ct);
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = sql;
        await cmd.ExecuteNonQueryAsync(ct);

        try
        {
            using var ckp2 = _conn.CreateCommand();
            ckp2.CommandText = "PRAGMA wal_checkpoint(TRUNCATE);";
            await ckp2.ExecuteNonQueryAsync(ct);
        }
        catch { }
    }

    public void Dispose() => _conn?.Dispose();

    private long NowMs() => _time.GetUtcNow().ToUnixTimeMilliseconds();

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

    // Operation APIs mapped to operations table
    public async Task<long> StartOperationAsync(long sessionId, string type, string? name = null, string? metadataJson = null, long startedAtMs = 0, CancellationToken ct = default)
    {
        using var cmd = _conn!.CreateCommand();
        cmd.CommandText = "INSERT INTO operations(session_id,type,status,started_at_ms,name,metadata_json) VALUES($s,$ty,'running',$t,$n,$m); SELECT last_insert_rowid();";
        cmd.Parameters.AddWithValue("$s", sessionId);
        cmd.Parameters.AddWithValue("$ty", type);
        cmd.Parameters.AddWithValue("$t", startedAtMs == 0 ? NowMs() : startedAtMs);
        cmd.Parameters.AddWithValue("$n", (object?)name ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$m", (object?)metadataJson ?? DBNull.Value);
        return (long)(await cmd.ExecuteScalarAsync(ct))!;
    }

    public async Task EndOperationAsync(long operationId, string status, long endedAtMs = 0, CancellationToken ct = default)
    {
        using var cmd = _conn!.CreateCommand();
        cmd.CommandText = "UPDATE operations SET status=$st, ended_at_ms=$t WHERE id=$id";
        cmd.Parameters.AddWithValue("$st", status);
        cmd.Parameters.AddWithValue("$t", endedAtMs == 0 ? NowMs() : endedAtMs);
        cmd.Parameters.AddWithValue("$id", operationId);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<long> UpsertItemAsync(long operationId, string externalId, string status, int highestSeverity = 2, long startedAtMs = 0, long endedAtMs = 0, string? metricsJson = null, CancellationToken ct = default)
    {
        using var tx = await _conn!.BeginTransactionAsync(ct);
        long id;
        using (var find = _conn.CreateCommand())
        {
            find.Transaction = (SqliteTransaction)tx;
            find.CommandText = "SELECT id FROM items WHERE operation_id=$r AND external_id=$e LIMIT 1";
            find.Parameters.AddWithValue("$r", operationId);
            find.Parameters.AddWithValue("$e", externalId);
            var res = await find.ExecuteScalarAsync(ct);
            id = res is long l ? l : 0L;
        }
        if (id == 0)
        {
            using var ins = _conn.CreateCommand();
            ins.Transaction = (SqliteTransaction)tx;
            ins.CommandText = "INSERT INTO items(operation_id, external_id, status, highest_severity, started_at_ms, ended_at_ms, metrics_json) VALUES($r,$e,$s,$h,$st,$en,$m); SELECT last_insert_rowid();";
            ins.Parameters.AddWithValue("$r", operationId);
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
        cmd.CommandText = @"INSERT INTO log_entries(ts_ms, level, category, subcategory, message, data_json, session_id, operation_id, item_id, source)
                             VALUES($ts,$lv,$c,$s,$m,$d,$sid,$oid,$iid,$src); SELECT last_insert_rowid();";
        cmd.Parameters.AddWithValue("$ts", log.TsMs);
        cmd.Parameters.AddWithValue("$lv", log.Level);
        cmd.Parameters.AddWithValue("$c", (object?)log.Category ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$s", (object?)log.Subcategory ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$m", (object?)log.Message ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$d", (object?)log.DataJson ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$sid", (object?)log.SessionId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$oid", (object?)log.OperationId ?? DBNull.Value);
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
        cmd.CommandText = $"SELECT id, ts_ms, level, category, subcategory, message, data_json, operation_id, item_id FROM log_entries {where} ORDER BY ts_ms LIMIT $lim OFFSET $off";
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
        if (q.OperationId is long oid) { parts.Add("operation_id=$oid"); pars.Add(new SqliteParameter("$oid", oid)); }
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
