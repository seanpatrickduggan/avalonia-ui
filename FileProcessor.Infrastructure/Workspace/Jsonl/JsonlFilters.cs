using FileProcessor.Core.Workspace;

namespace FileProcessor.Infrastructure.Workspace.Jsonl;

internal static class JsonlFilters
{
    internal sealed class FilterChain
    {
        private readonly System.Collections.Generic.List<Func<JsonlLineParser.ParsedLog, bool>> _filters = new(8);
        public void Add(Func<JsonlLineParser.ParsedLog, bool> f) => _filters.Add(f);
        public bool Matches(JsonlLineParser.ParsedLog p)
        {
            for (int i = 0; i < _filters.Count; i++) if (!_filters[i](p)) return false;
            return true;
        }
        public Func<JsonlLineParser.ParsedLog, bool> ToPredicate() => _filters.Count == 0 ? static _ => true : Matches;
    }

    public static Func<JsonlLineParser.ParsedLog, bool> BuildPredicate(LogQuery q)
    {
        var chain = new FilterChain();
        AddIdFilters(q, chain);
        AddLevelFilters(q, chain);
        AddCategoryFilters(q, chain);
        AddTimeFilters(q, chain);
        AddTextFilter(q, chain);
        return chain.ToPredicate();
    }

    private static void AddIdFilters(in LogQuery q, FilterChain chain)
    {
        if (q.OperationId is long oid) chain.Add(p => p.OperationId == oid);
        if (q.ItemId is long iid) chain.Add(p => p.ItemId == iid);
    }

    private static void AddLevelFilters(in LogQuery q, FilterChain chain)
    {
        if (q.MinLevel is int min) chain.Add(p => p.Level >= min);
        if (q.MaxLevel is int max) chain.Add(p => p.Level <= max);
    }

    private static void AddCategoryFilters(in LogQuery q, FilterChain chain)
    {
        if (!string.IsNullOrWhiteSpace(q.Category))
        {
            var cat = q.Category!;
            chain.Add(p => string.Equals(p.Category, cat));
        }
        if (!string.IsNullOrWhiteSpace(q.Subcategory))
        {
            var sub = q.Subcategory!;
            chain.Add(p => string.Equals(p.Subcategory, sub));
        }
    }

    private static void AddTimeFilters(in LogQuery q, FilterChain chain)
    {
        if (q.FromTsMs is long from) chain.Add(p => p.Ts >= from);
        if (q.ToTsMs is long to) chain.Add(p => p.Ts <= to);
    }

    private static void AddTextFilter(in LogQuery q, FilterChain chain)
    {
        if (!string.IsNullOrWhiteSpace(q.TextContains))
        {
            var text = q.TextContains!;
            chain.Add(p => p.Message != null && p.Message.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0);
        }
    }
}
