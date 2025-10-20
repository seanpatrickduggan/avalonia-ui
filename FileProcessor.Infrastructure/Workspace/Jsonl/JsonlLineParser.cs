using System;
using System.Text.Json;

namespace FileProcessor.Infrastructure.Workspace.Jsonl;

internal static class JsonlLineParser
{
    internal readonly record struct ParsedLog(
        long Ts,
        int Level,
        string? Category,
        string? Subcategory,
        string? Message,
        string? DataJson,
        long? OperationId,
        long? ItemId);

    private static string? GetStringProp(JsonElement root, string name)
        => root.TryGetProperty(name, out var el) && el.ValueKind == JsonValueKind.String ? el.GetString() : null;
    private static long? GetInt64Prop(JsonElement root, string name)
        => root.TryGetProperty(name, out var el) && el.TryGetInt64(out var v) ? v : null;
    private static int? GetInt32Prop(JsonElement root, string name)
        => root.TryGetProperty(name, out var el) && el.TryGetInt32(out var v) ? v : null;
    private static string? GetRawProp(JsonElement root, string name)
        => root.TryGetProperty(name, out var el) ? el.GetRawText() : null;

    public static bool TryParseLine(string line, out ParsedLog parsed)
    {
        parsed = default;
        try
        {
            using var doc = JsonDocument.Parse(line);
            var root = doc.RootElement;
            var ts = GetInt64Prop(root, "ts_ms") ?? 0L;
            var lvl = GetInt32Prop(root, "level") ?? 2;
            var cat = GetStringProp(root, "category");
            var sub = GetStringProp(root, "subcategory");
            var msg = GetStringProp(root, "message");
            var data = GetRawProp(root, "data");
            var opId = GetInt64Prop(root, "operation_id");
            var itemId = GetInt64Prop(root, "item_id");
            parsed = new ParsedLog(ts, lvl, cat, sub, msg, data, opId, itemId);
            return true;
        }
        catch { return false; }
    }
}
