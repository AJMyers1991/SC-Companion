using SCCompanion.Data;

namespace SCCompanion.Tests.Data;

[TestClass]
public sealed class AppDatabaseSelectionsTests
{
    [TestMethod]
    public async Task SetSelectionAsync_StoresAndRetrievesSelection()
    {
        await using var database = new AppDatabase(":memory:");
        await database.InitializeAsync();

        await database.SetSelectionAsync("trade", "origin", "Area18");

        string? selectedValue = await database.GetSelectionAsync("trade", "origin");
        Assert.AreEqual("Area18", selectedValue);
    }

    [TestMethod]
    public async Task SetSelectionAsync_ReplacesExistingSelection()
    {
        await using var database = new AppDatabase(":memory:");
        await database.InitializeAsync();

        await database.SetSelectionAsync("trade", "origin", "Area18");
        await database.SetSelectionAsync("trade", "origin", "Orison");

        string? selectedValue = await database.GetSelectionAsync("trade", "origin");
        Assert.AreEqual("Orison", selectedValue);
    }

    [TestMethod]
    public async Task RemoveSelectionAsync_ClearsStoredSelection()
    {
        await using var database = new AppDatabase(":memory:");
        await database.InitializeAsync();
        await database.SetSelectionAsync("trade", "origin", "Area18");

        await database.RemoveSelectionAsync("trade", "origin");

        string? selectedValue = await database.GetSelectionAsync("trade", "origin");
        Assert.IsNull(selectedValue);
    }
}
