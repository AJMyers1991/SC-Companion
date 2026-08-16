using SCCompanion.Data;

namespace SCCompanion.Tests.Ships;

[TestClass]
public sealed class ShipFleetDatabaseTests
{
    [TestMethod]
    public async Task FleetMembership_PersistsAndCanBeRemoved()
    {
        string databasePath = Path.Combine(
            Path.GetTempPath(),
            $"sccompanion-fleet-{Guid.NewGuid():N}.db3");

        try
        {
            await using (var database = new AppDatabase(databasePath))
            {
                await database.SetShipFleetMembershipAsync("ship-1", "Carrack", true);
                Assert.IsTrue(await database.IsShipInFleetAsync("ship-1"));
            }

            await using (var reopened = new AppDatabase(databasePath))
            {
                IReadOnlySet<string> fleet = await reopened.GetShipFleetIdsAsync();
                CollectionAssert.Contains(fleet.ToArray(), "ship-1");
                await reopened.SetShipFleetMembershipAsync("ship-1", "Carrack", false);
                Assert.IsFalse(await reopened.IsShipInFleetAsync("ship-1"));
            }
        }
        finally
        {
            if (File.Exists(databasePath))
            {
                File.Delete(databasePath);
            }
        }
    }

    [TestMethod]
    public async Task ConcurrentFavoriteToggles_AreSerializedAndRemainConsistent()
    {
        string databasePath = Path.Combine(
            Path.GetTempPath(),
            $"sccompanion-favorites-{Guid.NewGuid():N}.db3");

        try
        {
            await using var database = new AppDatabase(databasePath);
            Task<bool>[] toggles = Enumerable.Range(0, 10)
                .Select(_ => database.ToggleFavoriteAsync("ship", "ship-1", "Carrack"))
                .ToArray();

            await Task.WhenAll(toggles);

            Assert.IsEmpty(await database.GetFavoritesAsync("ship"));
        }
        finally
        {
            if (File.Exists(databasePath)) File.Delete(databasePath);
        }
    }

    [TestMethod]
    public async Task ConcurrentFleetAdds_AreSerializedAndRemainConsistent()
    {
        string databasePath = Path.Combine(
            Path.GetTempPath(),
            $"sccompanion-fleet-concurrent-{Guid.NewGuid():N}.db3");

        try
        {
            await using var database = new AppDatabase(databasePath);
            Task[] writes = Enumerable.Range(0, 10)
                .Select(_ => database.SetShipFleetMembershipAsync("ship-1", "Carrack", true))
                .ToArray();

            await Task.WhenAll(writes);

            IReadOnlySet<string> fleet = await database.GetShipFleetIdsAsync();
            Assert.HasCount(1, fleet);
            CollectionAssert.Contains(fleet.ToArray(), "ship-1");
        }
        finally
        {
            if (File.Exists(databasePath)) File.Delete(databasePath);
        }
    }
}
