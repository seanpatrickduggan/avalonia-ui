namespace FileProcessor.Core.Logging;

public static class ItemLogExtensions
{
    private static void Write(IItemLogScope scope, LogSeverity level, string message, object? data, string? category, string? subcategory)
    {
        // Default category/subcategory logic:
        var cat = string.IsNullOrWhiteSpace(category) ? "process" : category;
        var sub = string.IsNullOrWhiteSpace(subcategory) ? scope.ItemId : subcategory!;
        scope.Log(level, cat, sub, message, data);
    }

    public static void Trace(this IItemLogScope scope, string message, object? data = null, string? category = null, string? subcategory = null) => Write(scope, LogSeverity.Trace, message, data, category, subcategory);
    public static void Debug(this IItemLogScope scope, string message, object? data = null, string? category = null, string? subcategory = null) => Write(scope, LogSeverity.Debug, message, data, category, subcategory);
    public static void Info(this IItemLogScope scope, string message, object? data = null, string? category = null, string? subcategory = null) => Write(scope, LogSeverity.Info, message, data, category, subcategory);
    public static void Warning(this IItemLogScope scope, string message, object? data = null, string? category = null, string? subcategory = null) => Write(scope, LogSeverity.Warning, message, data, category, subcategory);
    public static void Warn(this IItemLogScope scope, string message, object? data = null, string? category = null, string? subcategory = null) => Write(scope, LogSeverity.Warning, message, data, category, subcategory);
    public static void Error(this IItemLogScope scope, string message, object? data = null, string? category = null, string? subcategory = null) => Write(scope, LogSeverity.Error, message, data, category, subcategory);
    public static void Critical(this IItemLogScope scope, string message, object? data = null, string? category = null, string? subcategory = null) => Write(scope, LogSeverity.Critical, message, data, category, subcategory);

    // Convenience timing helper
    public static T Timed<T>(this IItemLogScope scope, string operation, Func<T> action, string? category = null, string? subcategory = null)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var result = action();
            sw.Stop();
            scope.Debug($"{operation} completed", new { durationMs = sw.ElapsedMilliseconds }, category, subcategory);
            return result;
        }
        catch (Exception ex)
        {
            sw.Stop();
            scope.Error($"{operation} failed: {ex.Message}", new { durationMs = sw.ElapsedMilliseconds, ex.Message, ex.StackTrace }, category, subcategory);
            throw;
        }
    }

    public static void Timed(this IItemLogScope scope, string operation, Action action, string? category = null, string? subcategory = null)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            action();
            sw.Stop();
            scope.Debug($"{operation} completed", new { durationMs = sw.ElapsedMilliseconds }, category, subcategory);
        }
        catch (Exception ex)
        {
            sw.Stop();
            scope.Error($"{operation} failed: {ex.Message}", new { durationMs = sw.ElapsedMilliseconds, ex.Message, ex.StackTrace }, category, subcategory);
            throw;
        }
    }
}
