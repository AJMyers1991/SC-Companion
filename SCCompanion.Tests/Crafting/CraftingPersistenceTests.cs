using SCCompanion.Data;
using SCCompanion.Data.Entities;

namespace SCCompanion.Tests.Crafting;

[TestClass]
public sealed class CraftingPersistenceTests
{
    private string _databasePath = null!;
    private AppDatabase _database = null!;

    [TestInitialize]
    public async Task InitializeAsync()
    {
        _databasePath = Path.Combine(
            Path.GetTempPath(),
            $"sccompanion-crafting-{Guid.NewGuid():N}.db3");
        _database = new AppDatabase(_databasePath);
        await _database.InitializeAsync();
    }

    [TestCleanup]
    public async Task DisposeAsync()
    {
        await _database.DisposeAsync();
        if (File.Exists(_databasePath)) File.Delete(_databasePath);
    }

    [TestMethod]
    public async Task RecentBlueprints_AreNewestFirstAndLimited()
    {
        for (int id = 1; id <= 6; id++)
        {
            await _database.SaveCraftingBlueprintSummaryAsync(
                new CraftingBlueprintSummaryRecord
                {
                    BlueprintId = id,
                    DisplayName = $"Item {id}",
                    Category = "Weapons"
                },
                markOpened: true);
        }

        IReadOnlyList<CraftingBlueprintSummaryRecord> recent =
            await _database.GetRecentCraftingBlueprintsAsync(5);

        CollectionAssert.AreEqual(
            new long[] { 6, 5, 4, 3, 2 },
            recent.Select(item => item.BlueprintId).ToArray());
    }

    [TestMethod]
    public async Task SavingFavoriteMetadata_DoesNotEraseRecentOrdering()
    {
        await _database.SaveCraftingBlueprintSummaryAsync(
            new CraftingBlueprintSummaryRecord
            {
                BlueprintId = 10,
                DisplayName = "Original",
                Category = "Weapons"
            },
            markOpened: true);
        await _database.SaveCraftingBlueprintSummaryAsync(
            new CraftingBlueprintSummaryRecord
            {
                BlueprintId = 10,
                DisplayName = "Updated",
                Category = "Weapons / Rifle"
            },
            markOpened: false);

        IReadOnlyList<CraftingBlueprintSummaryRecord> recent =
            await _database.GetRecentCraftingBlueprintsAsync(5);
        Assert.HasCount(1, recent);
        CraftingBlueprintSummaryRecord record = recent[0];
        Assert.AreEqual("Updated", record.DisplayName);
        Assert.IsNotNull(record.LastOpenedUtc);
    }
}
