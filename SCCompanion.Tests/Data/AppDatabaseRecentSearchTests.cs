using SCCompanion.Data;

namespace SCCompanion.Tests.Data;

[TestClass]
public sealed class AppDatabaseRecentSearchTests
{
    [TestMethod]
    public async Task AddRecentSearchAsync_StoresTrimmedQueryForFeature()
    {
        await using var database = new AppDatabase(":memory:");

        await database.AddRecentSearchAsync("finder", "  quantum drive  ");

        IReadOnlyList<string> searches = await database.GetRecentSearchesAsync("finder");

        CollectionAssert.AreEqual(
            new[] { "quantum drive" },
            searches.ToArray());
    }

    [TestMethod]
    public async Task AddRecentSearchAsync_MovesCaseInsensitiveDuplicateToTop()
    {
        await using var database = new AppDatabase(":memory:");

        await database.AddRecentSearchAsync("wiki", "Carrack");
        await database.AddRecentSearchAsync("wiki", "Cutlass");
        await database.AddRecentSearchAsync("wiki", "  carrack  ");

        IReadOnlyList<string> searches = await database.GetRecentSearchesAsync("wiki");

        CollectionAssert.AreEqual(
            new[] { "carrack", "Cutlass" },
            searches.ToArray());
    }

    [TestMethod]
    public async Task AddRecentSearchAsync_CapsEachFeatureAtTen()
    {
        await using var database = new AppDatabase(":memory:");

        for (int index = 0; index < 12; index++)
        {
            await database.AddRecentSearchAsync("finder", $"Finder {index}");
        }
        await database.AddRecentSearchAsync("wiki", "Carrack");

        IReadOnlyList<string> finderSearches = await database.GetRecentSearchesAsync("finder", 20);
        IReadOnlyList<string> wikiSearches = await database.GetRecentSearchesAsync("wiki", 20);

        Assert.HasCount(10, finderSearches);
        Assert.AreEqual("Finder 11", finderSearches[0]);
        Assert.AreEqual("Finder 2", finderSearches[^1]);
        CollectionAssert.AreEqual(new[] { "Carrack" }, wikiSearches.ToArray());
    }

    [TestMethod]
    public async Task RecentSearches_PersistWhenDatabaseIsReopened()
    {
        string databasePath = Path.Combine(
            Path.GetTempPath(),
            $"sccompanion-recent-{Guid.NewGuid():N}.db3");

        try
        {
            await using (var firstSession = new AppDatabase(databasePath))
            {
                await firstSession.AddRecentSearchAsync("wiki", "Constellation");
            }

            await using var secondSession = new AppDatabase(databasePath);
            IReadOnlyList<string> searches = await secondSession.GetRecentSearchesAsync("wiki");

            CollectionAssert.AreEqual(new[] { "Constellation" }, searches.ToArray());
        }
        finally
        {
            File.Delete(databasePath);
        }
    }
}
