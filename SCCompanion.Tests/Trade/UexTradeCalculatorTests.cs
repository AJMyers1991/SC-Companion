using SCCompanion.Data.Trade;

namespace SCCompanion.Tests.Trade;

[TestClass]
public sealed class UexTradeCalculatorTests
{
    [TestMethod]
    public void ComputeHotTrades_UsesPlayerPerspectiveAndLimitsResultsToThirty()
    {
        var entries = new List<UexPriceEntry>();
        for (int index = 0; index < 35; index++)
        {
            entries.Add(CreateEntry(
                $"Commodity {index:D2}",
                $"Buy {index:D2}",
                buyPrice: 100 + index,
                sellPrice: 0));
            entries.Add(CreateEntry(
                $"Commodity {index:D2}",
                $"Sell {index:D2}",
                buyPrice: 0,
                sellPrice: 200 + (index * 2)));
        }

        IReadOnlyList<HotTrade> trades = UexTradeCalculator.ComputeHotTrades(entries);

        Assert.HasCount(30, trades);
        Assert.AreEqual("Commodity 34", trades[0].Commodity);
        Assert.AreEqual("Buy 34", trades[0].BuyEntry.TerminalName);
        Assert.AreEqual("Sell 34", trades[0].SellEntry.TerminalName);
        Assert.AreEqual(134, trades[0].BuyPrice);
        Assert.AreEqual(268, trades[0].SellPrice);
    }

    [TestMethod]
    public void FindBuyAndSellResults_PreserveKotlinSortingRules()
    {
        UexPriceEntry[] entries =
        [
            CreateEntry("Zeta", "Selected Terminal", 20, 35),
            CreateEntry("Alpha", "Selected Terminal", 30, 45),
            CreateEntry("Beta", "Other Terminal", 10, 55)
        ];

        IReadOnlyList<TradeResult> buys = UexTradeCalculator.FindBuyResults(
            entries,
            null,
            "Selected Terminal");
        IReadOnlyList<TradeResult> sells = UexTradeCalculator.FindSellResults(
            entries,
            null,
            "Selected Terminal");

        CollectionAssert.AreEqual(
            new[] { "Alpha", "Zeta" },
            buys.Select(result => result.Commodity).ToArray());
        CollectionAssert.AreEqual(
            new[] { "Alpha", "Zeta" },
            sells.Select(result => result.Commodity).ToArray());
        Assert.AreEqual(45, sells[0].Price);
    }

    [TestMethod]
    public void FilterSuggestions_PutsStartsWithBeforeContains()
    {
        IReadOnlyList<string> matches = UexTradeCalculator.FilterSuggestions(
            ["Agricium", "Medical Supplies", "Astatine", "Diamond"],
            "a");

        CollectionAssert.AreEqual(
            new[] { "Agricium", "Astatine", "Diamond", "Medical Supplies" },
            matches.ToArray());
    }

    [TestMethod]
    public void StatusFlag_IsAdvisoryAndDoesNotRemovePositivePrice()
    {
        UexPriceEntry entry = CreateEntry(
            "Gold",
            "Terminal",
            buyPrice: 10,
            sellPrice: 20,
            buyStatus: 0,
            sellStatus: null);

        Assert.HasCount(1, UexTradeCalculator.FindBuyResults([entry], "Gold", null));
        Assert.HasCount(1, UexTradeCalculator.FindSellResults([entry], "Gold", null));
        Assert.IsTrue(UexTradeCalculator.HasUnreliableStatus(entry, TradeAction.Buy));
        Assert.IsTrue(UexTradeCalculator.HasUnreliableStatus(entry, TradeAction.Sell));
    }

    private static UexPriceEntry CreateEntry(
        string commodity,
        string terminal,
        double buyPrice,
        double sellPrice,
        int? buyStatus = 1,
        int? sellStatus = 1) =>
        new(
            Id: null,
            CommodityId: null,
            TerminalId: null,
            CommodityName: commodity,
            TerminalName: terminal,
            BuyPrice: buyPrice,
            AverageBuyPrice: buyPrice,
            SellPrice: sellPrice,
            AverageSellPrice: sellPrice,
            BuyQuantity: 10,
            AverageBuyQuantity: 15,
            SellStock: 20,
            AverageSellStock: 25,
            SellQuantity: 30,
            AverageSellQuantity: 35,
            BuyStatus: buyStatus,
            SellStatus: sellStatus,
            DateModified: 1_700_000_000);
}
