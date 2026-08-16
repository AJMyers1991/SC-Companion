using System.Globalization;
using SCCompanion.Data.Trade;

namespace SCCompanion.Models;

public sealed class TradeCardViewItem
{
    private TradeCardViewItem(
        string commodity,
        TradeResult? result,
        HotTrade? hotTrade)
    {
        Commodity = commodity;
        Result = result;
        HotTrade = hotTrade;
    }

    public TradeResult? Result { get; }

    public HotTrade? HotTrade { get; }

    public bool IsHotTrade => HotTrade is not null;

    public bool IsResult => Result is not null;

    public string Commodity { get; }

    public string ProfitText => HotTrade is null
        ? string.Empty
        : $"{FormatNumber(HotTrade.Profit)} / SCU Profit";

    public string BuyLocationText => HotTrade is null
        ? string.Empty
        : $"Buy: {HotTrade.BuyEntry.TerminalName}";

    public string BuyPriceText => HotTrade is null
        ? string.Empty
        : $"@ {FormatNumber(HotTrade.BuyPrice)}";

    public string SellLocationText => HotTrade is null
        ? string.Empty
        : $"Sell: {HotTrade.SellEntry.TerminalName}";

    public string SellPriceText => HotTrade is null
        ? string.Empty
        : $"@ {FormatNumber(HotTrade.SellPrice)}";

    public string ResultLocation => Result?.Location ?? string.Empty;

    public string ResultPriceText => Result is null
        ? string.Empty
        : FormatNumber(Result.Price);

    public string ResultActionText => Result?.Action == TradeAction.Buy
        ? "Buy from"
        : "Sell to";

    public static TradeCardViewItem FromResult(TradeResult result) =>
        new(result.Commodity, result, null);

    public static TradeCardViewItem FromHotTrade(HotTrade hotTrade) =>
        new(hotTrade.Commodity, null, hotTrade);

    private static string FormatNumber(double value) =>
        value.ToString("N0", CultureInfo.CurrentCulture);
}

public sealed record TradeInventorySectionViewItem(
    string Heading,
    string Location,
    string PriceText,
    string LastQuantityLabel,
    string LastQuantityText,
    string AverageQuantityLabel,
    string AverageQuantityText,
    string LastUpdatedText,
    bool HasStatusWarning,
    string StatusWarningText);
