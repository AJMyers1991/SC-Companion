namespace SCCompanion;

public static class FinderNavigationRequest
{
    private static readonly Lock Sync = new();
    private static string? _pendingQuery;

    public static void Set(string query)
    {
        lock (Sync) _pendingQuery = string.IsNullOrWhiteSpace(query) ? null : query.Trim();
    }

    public static string? Consume()
    {
        lock (Sync)
        {
            string? query = _pendingQuery;
            _pendingQuery = null;
            return query;
        }
    }
}
