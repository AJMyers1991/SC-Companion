namespace SCCompanion.Data.Wikelo;

/// <summary>
/// Performs deterministic local search over the loaded Wikelo trade catalog.
/// </summary>
public static class WikeloTradeSearchEngine
{
    public static IReadOnlyList<WikeloTrade> Search(
        IEnumerable<WikeloTrade> trades,
        string? query)
    {
        ArgumentNullException.ThrowIfNull(trades);

        string normalizedQuery = query?.Trim() ?? string.Empty;
        if (normalizedQuery.Length == 0)
        {
            return trades.ToArray();
        }

        return trades
            .Where(trade => Matches(trade, normalizedQuery))
            .ToArray();
    }

    private static bool Matches(WikeloTrade trade, string query)
    {
        return trade.MissionName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
               trade.RewardName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
               trade.RequiredItems.Any(item =>
                   item.Name.Contains(query, StringComparison.OrdinalIgnoreCase));
    }
}
