namespace SCCompanion.Data.Search;

/// <summary>
/// Represents one searchable CStone item.
/// </summary>
public sealed record FinderItem(string Id, string Name, bool IsAvailable);

/// <summary>
/// Applies the Kotlin Finder ordering contract to the downloaded item index.
/// </summary>
public static class FinderSearchEngine
{
    public static IReadOnlyList<FinderItem> Search(
        IEnumerable<FinderItem> items,
        string query,
        int limit = 50)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(query);
        ArgumentOutOfRangeException.ThrowIfLessThan(limit, 1);

        string normalizedQuery = query.Trim();
        if (normalizedQuery.Length < 2)
        {
            return [];
        }

        FinderItem[] itemArray = items.ToArray();
        IEnumerable<FinderItem> prefixMatches = itemArray.Where(item =>
            item.Name.StartsWith(normalizedQuery, StringComparison.OrdinalIgnoreCase));
        IEnumerable<FinderItem> containsMatches = itemArray.Where(item =>
            !item.Name.StartsWith(normalizedQuery, StringComparison.OrdinalIgnoreCase) &&
            item.Name.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase));

        return prefixMatches
            .Concat(containsMatches)
            .Take(limit)
            .ToArray();
    }
}
