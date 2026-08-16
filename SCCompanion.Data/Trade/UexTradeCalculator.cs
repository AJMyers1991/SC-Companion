namespace SCCompanion.Data.Trade;

/// <summary>
/// Applies the Kotlin Trade screen's filtering, sorting, autocomplete, and route rules.
/// </summary>
public static class UexTradeCalculator
{
    public const int MaximumHotTrades = 30;

    public static IReadOnlyList<string> GetCommodityNames(IEnumerable<UexPriceEntry> entries) =>
        GetDistinctNames(entries.Select(entry => entry.CommodityName));

    public static IReadOnlyList<string> GetTerminalNames(IEnumerable<UexPriceEntry> entries) =>
        GetDistinctNames(entries.Select(entry => entry.TerminalName));

    public static IReadOnlyList<string> FilterSuggestions(
        IEnumerable<string> values,
        string? query)
    {
        string normalizedQuery = query?.Trim() ?? string.Empty;
        string[] orderedValues = values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (normalizedQuery.Length == 0)
        {
            return orderedValues;
        }

        return orderedValues
            .Where(value => value.StartsWith(normalizedQuery, StringComparison.OrdinalIgnoreCase))
            .Concat(orderedValues.Where(value =>
                !value.StartsWith(normalizedQuery, StringComparison.OrdinalIgnoreCase) &&
                value.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase)))
            .ToArray();
    }

    public static IReadOnlyList<TradeResult> FindBuyResults(
        IEnumerable<UexPriceEntry> entries,
        string? commodity,
        string? terminal)
    {
        IEnumerable<TradeResult> results = FilterEntries(entries, commodity, terminal)
            .Where(entry => entry.BuyPrice is > 0)
            .Select(entry => new TradeResult(
                entry,
                TradeAction.Buy,
                entry.CommodityName ?? string.Empty,
                entry.TerminalName ?? string.Empty,
                entry.BuyPrice!.Value,
                entry.AverageBuyPrice is > 0
                    ? entry.AverageBuyPrice.Value
                    : entry.BuyPrice.Value));

        return IsTerminalOnly(commodity, terminal)
            ? results
                .OrderBy(result => result.Commodity, StringComparer.OrdinalIgnoreCase)
                .ThenBy(result => result.Price)
                .ToArray()
            : results.OrderBy(result => result.Price).ToArray();
    }

    public static IReadOnlyList<TradeResult> FindSellResults(
        IEnumerable<UexPriceEntry> entries,
        string? commodity,
        string? terminal)
    {
        IEnumerable<TradeResult> results = FilterEntries(entries, commodity, terminal)
            .Where(entry => entry.SellPrice is > 0)
            .Select(entry => new TradeResult(
                entry,
                TradeAction.Sell,
                entry.CommodityName ?? string.Empty,
                entry.TerminalName ?? string.Empty,
                entry.SellPrice!.Value,
                entry.AverageSellPrice is > 0
                    ? entry.AverageSellPrice.Value
                    : entry.SellPrice.Value));

        return IsTerminalOnly(commodity, terminal)
            ? results
                .OrderByDescending(result => result.Price)
                .ThenBy(result => result.Commodity, StringComparer.OrdinalIgnoreCase)
                .ToArray()
            : results.OrderByDescending(result => result.Price).ToArray();
    }

    public static IReadOnlyList<HotTrade> ComputeHotTrades(
        IEnumerable<UexPriceEntry> entries,
        int maximumCount = MaximumHotTrades)
    {
        if (maximumCount <= 0)
        {
            return [];
        }

        var trades = new List<HotTrade>();
        foreach (IGrouping<string, UexPriceEntry> commodityEntries in entries
                     .Where(entry => !string.IsNullOrWhiteSpace(entry.CommodityName))
                     .GroupBy(entry => entry.CommodityName!, StringComparer.OrdinalIgnoreCase))
        {
            UexPriceEntry? bestBuy = commodityEntries
                .Where(entry => entry.BuyPrice is > 0)
                .MinBy(entry => entry.BuyPrice);
            UexPriceEntry? bestSell = commodityEntries
                .Where(entry => entry.SellPrice is > 0)
                .MaxBy(entry => entry.SellPrice);

            if (bestBuy?.TerminalName is not { Length: > 0 } buyTerminal ||
                bestSell?.TerminalName is not { Length: > 0 } sellTerminal ||
                string.Equals(buyTerminal, sellTerminal, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            double buyPrice = bestBuy.BuyPrice!.Value;
            double sellPrice = bestSell.SellPrice!.Value;
            double profit = sellPrice - buyPrice;
            if (profit <= 0)
            {
                continue;
            }

            trades.Add(new HotTrade(
                commodityEntries.Key,
                bestBuy,
                bestSell,
                buyPrice,
                sellPrice,
                profit));
        }

        return trades
            .OrderByDescending(trade => trade.Profit)
            .Take(maximumCount)
            .ToArray();
    }

    public static bool HasUnreliableStatus(UexPriceEntry entry, TradeAction action) =>
        action == TradeAction.Buy
            ? entry.BuyStatus != 1
            : entry.SellStatus != 1;

    private static IReadOnlyList<string> GetDistinctNames(IEnumerable<string?> values) =>
        values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private static IEnumerable<UexPriceEntry> FilterEntries(
        IEnumerable<UexPriceEntry> entries,
        string? commodity,
        string? terminal)
    {
        string? normalizedCommodity = NullIfWhiteSpace(commodity);
        string? normalizedTerminal = NullIfWhiteSpace(terminal);

        return entries.Where(entry =>
            (normalizedCommodity is null || string.Equals(
                entry.CommodityName,
                normalizedCommodity,
                StringComparison.OrdinalIgnoreCase)) &&
            (normalizedTerminal is null || string.Equals(
                entry.TerminalName,
                normalizedTerminal,
                StringComparison.OrdinalIgnoreCase)));
    }

    private static bool IsTerminalOnly(string? commodity, string? terminal) =>
        string.IsNullOrWhiteSpace(commodity) && !string.IsNullOrWhiteSpace(terminal);

    private static string? NullIfWhiteSpace(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
