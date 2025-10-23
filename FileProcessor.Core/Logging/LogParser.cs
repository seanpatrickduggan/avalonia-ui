using System;
using System.Text.Json;

namespace FileProcessor.Core.Logging;

public static class LogParser
{
    /// <summary>
    /// Parse a single JSON line from Serilog/compact JSON or similar into an ItemLogEntry.
    /// Returns null when the line cannot be parsed.
    /// </summary>
    public static ItemLogEntry? Parse(string line)
    {
        try
        {
            using var doc = JsonDocument.Parse(line);
            var root = doc.RootElement;

            // Timestamp: handle Serilog '@t' as well as 'Timestamp'
            DateTime ts = DateTime.UtcNow;
            if (root.TryGetProperty("Timestamp", out var tEl) || root.TryGetProperty("@t", out tEl))
            {
                if (tEl.ValueKind == JsonValueKind.String)
                {
                    var s = tEl.GetString();
                    if (!string.IsNullOrEmpty(s) && DateTime.TryParse(s, out var parsed)) ts = parsed;
                }
                else if (tEl.ValueKind == JsonValueKind.Number && tEl.TryGetDateTime(out var parsedNum))
                {
                    ts = parsedNum;
                }
            }

            // Message: prefer RenderedMessage or Message, fall back to templates
            var msg = string.Empty;
            if (root.TryGetProperty("RenderedMessage", out var rmEl)) msg = rmEl.GetString() ?? string.Empty;
            else if (root.TryGetProperty("Message", out var mEl)) msg = mEl.GetString() ?? string.Empty;
            else if (root.TryGetProperty("@mt", out var mtEl)) msg = mtEl.GetString() ?? string.Empty;
            else if (root.TryGetProperty("MessageTemplate", out var mt2El)) msg = mt2El.GetString() ?? string.Empty;

            // Level: handle '@l' (Serilog) or 'Level'
            var levelStr = "Information";
            if (root.TryGetProperty("Level", out var lvlEl) && lvlEl.ValueKind == JsonValueKind.String) levelStr = lvlEl.GetString() ?? levelStr;
            else if (root.TryGetProperty("@l", out var lvl2El) && lvl2El.ValueKind == JsonValueKind.String) levelStr = lvl2El.GetString() ?? levelStr;

            LogSeverity severity = levelStr switch { "Verbose" => LogSeverity.Trace, "Debug" => LogSeverity.Debug, "Information" => LogSeverity.Info, "Warning" => LogSeverity.Warning, "Error" => LogSeverity.Error, "Fatal" => LogSeverity.Critical, _ => LogSeverity.Info };

            string cat = string.Empty; string sub = string.Empty; string? dataStr = null;

            // Top-level cat/sub/severityRank/data or inside Properties
            if (root.TryGetProperty("cat", out var catEl)) cat = catEl.ToString();
            if (root.TryGetProperty("sub", out var subEl)) sub = subEl.ToString();

            if (root.TryGetProperty("severityRank", out var rankEl))
            {
                if (rankEl.ValueKind == JsonValueKind.Number)
                {
                    try
                    {
                        var rank = rankEl.GetInt32();
                        if (rank >= 0 && rank <= 5) severity = (LogSeverity)rank;
                    }
                    catch { }
                }
            }

            if (root.TryGetProperty("Data", out var dataEl))
            {
                // Capture raw JSON text for the Data property (preserve structured payload)
                dataStr = dataEl.GetRawText();
            }

            if (root.TryGetProperty("Properties", out var props))
            {
                if (string.IsNullOrEmpty(cat) && props.TryGetProperty("cat", out var cat2)) cat = cat2.ToString();
                if (string.IsNullOrEmpty(sub) && props.TryGetProperty("sub", out var sub2)) sub = sub2.ToString();
                if (props.TryGetProperty("severityRank", out var rank2))
                {
                    try
                    {
                        var rank = rank2.GetInt32();
                        if (rank >= 0 && rank <= 5) severity = (LogSeverity)rank;
                    }
                    catch { }
                }
                if (dataStr == null && props.TryGetProperty("data", out var data2)) dataStr = data2.GetRawText();
            }

            return new ItemLogEntry(ts, severity, cat, sub, msg, dataStr);
        }
        catch
        {
            return null;
        }
    }
}
