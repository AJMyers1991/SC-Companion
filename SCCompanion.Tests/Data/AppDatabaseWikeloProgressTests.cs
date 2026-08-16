using SCCompanion.Data;

namespace SCCompanion.Tests.Data;

[TestClass]
public sealed class AppDatabaseWikeloProgressTests
{
    [TestMethod]
    public async Task SetWikeloTradeProgressAsync_UpsertsAndRemovesZeroQuantity()
    {
        await using var database = new AppDatabase(":memory:");

        await database.SetWikeloTradeProgressAsync("trade-1", "item-1", 12);
        await database.SetWikeloTradeProgressAsync("trade-1", "item-1", 20);

        var progress = await database.GetWikeloTradeProgressAsync("trade-1");
        Assert.HasCount(1, progress);
        Assert.AreEqual(20, progress[0].OwnedQuantity);

        await database.SetWikeloTradeProgressAsync("trade-1", "item-1", 0);
        progress = await database.GetWikeloTradeProgressAsync("trade-1");
        Assert.IsEmpty(progress);
    }

    [TestMethod]
    public async Task DeleteWikeloTradeProgressAsync_RemovesOnlySelectedTrade()
    {
        await using var database = new AppDatabase(":memory:");
        await database.SetWikeloTradeProgressAsync("trade-1", "item-1", 5);
        await database.SetWikeloTradeProgressAsync("trade-2", "item-1", 7);

        await database.DeleteWikeloTradeProgressAsync("trade-1");

        Assert.IsEmpty(await database.GetWikeloTradeProgressAsync("trade-1"));
        Assert.HasCount(1, await database.GetWikeloTradeProgressAsync("trade-2"));
    }
}
