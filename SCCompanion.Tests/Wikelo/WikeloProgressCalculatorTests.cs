using SCCompanion.Data.Entities;
using SCCompanion.Data.Wikelo;

namespace SCCompanion.Tests.Wikelo;

[TestClass]
public sealed class WikeloProgressCalculatorTests
{
    private static readonly WikeloTrade Trade = new(
        "trade-1",
        "Test Trade",
        "Test Reward",
        "Test",
        "4.8.1",
        "New Customer",
        [
            new WikeloRequiredItem("partial", "Partial Item", 50, 0),
            new WikeloRequiredItem("untouched", "Untouched Item", 10, 1),
            new WikeloRequiredItem("complete", "Complete Item", 5, 2),
            new WikeloRequiredItem("partial-two", "Second Partial", 20, 3)
        ],
        string.Empty,
        true);

    [TestMethod]
    public void Calculate_UsesWeightedItemQuantities()
    {
        WikeloTradeProgress progress = WikeloProgressCalculator.Calculate(
            Trade,
            [
                Progress("partial", 25),
                Progress("complete", 5),
                Progress("partial-two", 10)
            ]);

        Assert.AreEqual(85, progress.TotalRequired);
        Assert.AreEqual(40, progress.TotalOwned);
        Assert.AreEqual(47, progress.Percentage);
    }

    [TestMethod]
    public void OrderRequiredItems_PutsPartialFirstUntouchedNextAndCompleteLast()
    {
        WikeloTradeProgress progress = WikeloProgressCalculator.Calculate(
            Trade,
            [
                Progress("partial", 25),
                Progress("complete", 5),
                Progress("partial-two", 10)
            ]);

        IReadOnlyList<WikeloItemProgress> ordered =
            WikeloProgressCalculator.OrderRequiredItems(progress);

        CollectionAssert.AreEqual(
            new[] { "partial", "partial-two", "untouched", "complete" },
            ordered.Select(item => item.Item.Id).ToArray());
    }

    [TestMethod]
    public void AggregateInventory_SumsMatchingItemsAcrossFavoriteTrades()
    {
        WikeloTrade secondTrade = Trade with
        {
            Id = "trade-2",
            RequiredItems =
            [
                new WikeloRequiredItem("partial", "Partial Item", 50, 0)
            ]
        };

        WikeloTradeProgress first = WikeloProgressCalculator.Calculate(
            Trade,
            [Progress("partial", 25)]);
        WikeloTradeProgress second = WikeloProgressCalculator.Calculate(
            secondTrade,
            [new WikeloTradeProgressRecord
            {
                TradeId = "trade-2",
                IngredientId = "partial",
                OwnedQuantity = 8
            }]);

        IReadOnlyList<WikeloInventoryItem> inventory =
            WikeloProgressCalculator.AggregateInventory([first, second]);

        Assert.HasCount(1, inventory);
        Assert.AreEqual("Partial Item", inventory[0].DisplayName);
        Assert.AreEqual(33, inventory[0].OwnedQuantity);
    }

    private static WikeloTradeProgressRecord Progress(string ingredientId, int quantity)
    {
        return new WikeloTradeProgressRecord
        {
            TradeId = Trade.Id,
            IngredientId = ingredientId,
            OwnedQuantity = quantity
        };
    }
}
