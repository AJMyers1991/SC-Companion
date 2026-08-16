using SCCompanion.Data.Wikelo;

namespace SCCompanion.Tests.Wikelo;

[TestClass]
public sealed class WikeloTradeServiceTests
{
    [TestMethod]
    public void ParseTradeScript_ParsesJavaScriptObjectsAndRewardArrays()
    {
        const string script = """
            window.trades = [
              {
                id: "special_1",
                missionName: "A Special Trade",
                rewardName: ["Special Helmet", "Special Armor"],
                category: "Armor",
                patch: "4.8.1",
                reputation: "Experienced Customer",
                requiredItems: [
                  { quantity: 25, items: "Wikelo Favor" },
                  { quantity: 2, items: "MG Scrip" },
                ],
                description: "",
                active: true,
              },
            ];
            """;

        IReadOnlyList<WikeloTrade> trades =
            WikeloTradeService.ParseTradeScript(script);

        Assert.HasCount(1, trades);
        WikeloTrade trade = trades[0];
        Assert.AreEqual("A Special Trade", trade.MissionName);
        Assert.AreEqual("Special Helmet, Special Armor", trade.RewardName);
        Assert.AreEqual("Experienced Customer", trade.RequiredReputation);
        Assert.HasCount(2, trade.RequiredItems);
        Assert.AreEqual("wikelo-favor", trade.RequiredItems[0].Id);
        Assert.AreEqual(25, trade.RequiredItems[0].RequiredQuantity);
    }

    [TestMethod]
    public void ParseTradeScript_ReturnsEmptyForMissingTradeArray()
    {
        IReadOnlyList<WikeloTrade> trades =
            WikeloTradeService.ParseTradeScript("window.trades = null;");

        Assert.IsEmpty(trades);
    }
}
