using SCCompanion.Data;
using SCCompanion.Data.Entities;

namespace SCCompanion.Tests.Data;

[TestClass]
public sealed class AppDatabaseFavoritesTests
{
    [TestMethod]
    public async Task SaveFavoriteAsync_StoresAndRetrievesFavorite()
    {
        await using var database = new AppDatabase(":memory:");
        await database.InitializeAsync();

        await database.SaveFavoriteAsync(new FavoriteRecord
        {
            Category = "ship",
            ExternalId = "anvil-carrack",
            DisplayName = "Carrack"
        });

        IReadOnlyList<FavoriteRecord> favorites = await database.GetFavoritesAsync("ship");

        Assert.HasCount(1, favorites);
        Assert.AreEqual("anvil-carrack", favorites[0].ExternalId);
        Assert.AreEqual("Carrack", favorites[0].DisplayName);
    }

    [TestMethod]
    public async Task SaveFavoriteAsync_UpdatesExistingFavoriteWithoutDuplicatingIt()
    {
        await using var database = new AppDatabase(":memory:");
        await database.InitializeAsync();

        await database.SaveFavoriteAsync(new FavoriteRecord
        {
            Category = "item",
            ExternalId = "item-42",
            DisplayName = "Old name"
        });
        await database.SaveFavoriteAsync(new FavoriteRecord
        {
            Category = "item",
            ExternalId = "item-42",
            DisplayName = "Updated name"
        });

        IReadOnlyList<FavoriteRecord> favorites = await database.GetFavoritesAsync("item");

        Assert.HasCount(1, favorites);
        Assert.AreEqual("Updated name", favorites[0].DisplayName);
    }

    [TestMethod]
    public async Task RemoveFavoriteAsync_RemovesOnlyTheMatchingFavorite()
    {
        await using var database = new AppDatabase(":memory:");
        await database.InitializeAsync();

        await database.SaveFavoriteAsync(new FavoriteRecord
        {
            Category = "ship",
            ExternalId = "anvil-carrack",
            DisplayName = "Carrack"
        });
        await database.SaveFavoriteAsync(new FavoriteRecord
        {
            Category = "ship",
            ExternalId = "misc-freelancer",
            DisplayName = "Freelancer"
        });

        await database.RemoveFavoriteAsync("ship", "anvil-carrack");

        IReadOnlyList<FavoriteRecord> favorites = await database.GetFavoritesAsync("ship");
        Assert.HasCount(1, favorites);
        Assert.AreEqual("misc-freelancer", favorites[0].ExternalId);
    }

    [TestMethod]
    public async Task ToggleFavoriteAsync_AddsMissingFavoriteAndReturnsSelectedState()
    {
        await using var database = new AppDatabase(":memory:");

        bool isFavorite = await database.ToggleFavoriteAsync(
            "useful-link",
            "https://erkul.games/calculator",
            "Erkul Ship Configurator");

        IReadOnlyList<FavoriteRecord> favorites = await database.GetFavoritesAsync("useful-link");

        Assert.IsTrue(isFavorite);
        Assert.HasCount(1, favorites);
        Assert.AreEqual("https://erkul.games/calculator", favorites[0].ExternalId);
    }

    [TestMethod]
    public async Task ToggleFavoriteAsync_RemovesExistingFavoriteAndReturnsUnselectedState()
    {
        await using var database = new AppDatabase(":memory:");
        await database.SaveFavoriteAsync(new FavoriteRecord
        {
            Category = "useful-link",
            ExternalId = "https://scdb.space",
            DisplayName = "SCDB"
        });

        bool isFavorite = await database.ToggleFavoriteAsync(
            "useful-link",
            "https://scdb.space",
            "SCDB");

        IReadOnlyList<FavoriteRecord> favorites = await database.GetFavoritesAsync("useful-link");

        Assert.IsFalse(isFavorite);
        Assert.IsEmpty(favorites);
    }
}
