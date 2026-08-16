using SCCompanion.Data.Entities;

namespace SCCompanion.Data.Wikelo;

/// <summary>
/// Calculates weighted trade completion and aggregated inventory state.
/// </summary>
public static class WikeloProgressCalculator
{
    public static WikeloTradeProgress Calculate(
        WikeloTrade trade,
        IEnumerable<WikeloTradeProgressRecord> progressRecords)
    {
        ArgumentNullException.ThrowIfNull(trade);
        ArgumentNullException.ThrowIfNull(progressRecords);

        Dictionary<string, int> quantities = progressRecords
            .Where(record => string.Equals(
                record.TradeId,
                trade.Id,
                StringComparison.OrdinalIgnoreCase))
            .GroupBy(record => record.IngredientId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.Last().OwnedQuantity,
                StringComparer.OrdinalIgnoreCase);

        WikeloItemProgress[] items = trade.RequiredItems
            .Select(item => new WikeloItemProgress(
                item,
                quantities.GetValueOrDefault(item.Id)))
            .ToArray();

        int totalRequired = items.Sum(item => Math.Max(0, item.Item.RequiredQuantity));
        int totalOwned = items.Sum(item => item.ClampedOwnedQuantity);
        double fraction = totalRequired == 0
            ? 0d
            : Math.Clamp((double)totalOwned / totalRequired, 0d, 1d);

        return new WikeloTradeProgress(
            trade,
            items,
            totalRequired,
            totalOwned,
            fraction);
    }

    public static IReadOnlyList<WikeloItemProgress> OrderRequiredItems(
        WikeloTradeProgress progress)
    {
        ArgumentNullException.ThrowIfNull(progress);

        return progress.Items
            .OrderBy(item => item.IsPartial ? 0 : item.IsUntouched ? 1 : 2)
            .ThenByDescending(item => item.IsPartial ? item.ClampedOwnedQuantity : int.MinValue)
            .ThenBy(item => item.Item.SourceOrder)
            .ToArray();
    }

    public static IReadOnlyList<WikeloInventoryItem> AggregateInventory(
        IEnumerable<WikeloTradeProgress> favoriteProgress)
    {
        ArgumentNullException.ThrowIfNull(favoriteProgress);

        return favoriteProgress
            .SelectMany(progress => progress.Items)
            .Where(item => item.ClampedOwnedQuantity > 0)
            .GroupBy(
                item => NormalizeIngredientKey(item.Item.Name),
                StringComparer.OrdinalIgnoreCase)
            .Select(group => new WikeloInventoryItem(
                group.Key,
                group.First().Item.Name,
                group.Sum(item => item.ClampedOwnedQuantity)))
            .OrderBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string NormalizeIngredientKey(string name)
    {
        return string.Join(
            '-',
            name.Trim()
                .ToLowerInvariant()
                .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }
}
