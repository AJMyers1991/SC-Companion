using SCCompanion.Data.Wikelo;

namespace SCCompanion.Tests.Wikelo;

[TestClass]
public sealed class WikeloTradeSearchEngineTests
{
    private static readonly WikeloTrade Trade = new(
        "trade-1",
        "Trade Scrip for Armor",
        "Palatino Helmet",
        "Armor",
        "4.8.1",
        "Experienced Customer",
        [
            new WikeloRequiredItem("mg-scrip", "MG Scrip", 50, 0),
            new WikeloRequiredItem("favors", "Wikelo Favor", 10, 1)
        ],
        string.Empty,
        true);

    [TestMethod]
    [DataRow("armor")]
    [DataRow("palatino")]
    [DataRow("mg scrip")]
    [DataRow("favor")]
    public void Search_MatchesMissionRewardAndRequiredItems(string query)
    {
        IReadOnlyList<WikeloTrade> matches =
            WikeloTradeSearchEngine.Search([Trade], query);

        Assert.HasCount(1, matches);
    }

    [TestMethod]
    public void Search_ExcludesUnrelatedTrade()
    {
        IReadOnlyList<WikeloTrade> matches =
            WikeloTradeSearchEngine.Search([Trade], "tractor beam");

        Assert.IsEmpty(matches);
    }
}
