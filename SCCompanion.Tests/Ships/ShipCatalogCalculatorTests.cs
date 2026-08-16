using SCCompanion.Data.Ships;

namespace SCCompanion.Tests.Ships;

[TestClass]
public sealed class ShipCatalogCalculatorTests
{
    [TestMethod]
    public void Search_PrioritizesPrefixMatchesBeforeContainsMatches()
    {
        FleetYardsShip[] ships =
        [
            Ship("Carrack"),
            Ship("Aegis Carrack Test"),
            Ship("Caterpillar")
        ];

        IReadOnlyList<FleetYardsShip> results = ShipCatalogCalculator.Apply(
            ships,
            "car",
            new ShipFilters(),
            new HashSet<string>(),
            new HashSet<string>());

        CollectionAssert.AreEqual(
            new[] { "Carrack", "Aegis Carrack Test" },
            results.Select(ship => ship.Name).ToArray());
    }

    [TestMethod]
    public void Filters_UseOrWithinDimensionsAndAndAcrossDimensions()
    {
        FleetYardsShip[] ships =
        [
            Ship("Arrow", manufacturer: "Aegis", focus: "Light Fighter", status: "flight-ready"),
            Ship("Gladius", manufacturer: "Anvil", focus: "Light Fighter", status: "flight-ready"),
            Ship("Concept Fighter", manufacturer: "Anvil", focus: "Heavy Fighter", status: "in-concept"),
            Ship("Freelancer", manufacturer: "MISC", focus: "Medium Freight", status: "flight-ready")
        ];
        var filters = new ShipFilters(
            Manufacturers: new HashSet<string>(["Aegis", "Anvil"]),
            Roles: new HashSet<string>(["Combat"]),
            ProductionStatuses: new HashSet<string>(["flight-ready"]));

        IReadOnlyList<FleetYardsShip> results = ShipCatalogCalculator.Apply(
            ships, string.Empty, filters, new HashSet<string>(), new HashSet<string>());

        CollectionAssert.AreEqual(
            new[] { "Arrow", "Gladius" },
            results.Select(ship => ship.Name).ToArray());
    }

    [TestMethod]
    public void Classification_SeparatesGroundVehiclesFromShipsUsingKotlinLabels()
    {
        FleetYardsShip[] ships =
        [
            Ship("Ursa", classification: "Vehicle"),
            Ship("Cyclone", classification: "ground vehicle"),
            Ship("Aurora", classification: "Ship")
        ];
        var filters = new ShipFilters(
            Classifications: new HashSet<string>(["ground_vehicle"]));

        IReadOnlyList<FleetYardsShip> results = ShipCatalogCalculator.Apply(
            ships, string.Empty, filters, new HashSet<string>(), new HashSet<string>());

        CollectionAssert.AreEqual(
            new[] { "Ursa", "Cyclone" },
            results.Select(ship => ship.Name).ToArray());
    }

    [TestMethod]
    public void OtherRole_CatchesOnlyFocusValuesOutsideNamedBuckets()
    {
        FleetYardsShip[] ships =
        [
            Ship("Reporter", focus: "Reporting"),
            Ship("Agricultural", focus: "Agriculture"),
            Ship("Miner", focus: "Mining")
        ];
        var filters = new ShipFilters(Roles: new HashSet<string>(["Other"]));

        IReadOnlyList<FleetYardsShip> results = ShipCatalogCalculator.Apply(
            ships, string.Empty, filters, new HashSet<string>(), new HashSet<string>());

        CollectionAssert.AreEqual(
            new[] { "Agricultural" },
            results.Select(ship => ship.Name).ToArray());
    }

    [TestMethod]
    public void PriceCargoFavoriteAndFleetFiltersMatchKotlinSemantics()
    {
        FleetYardsShip[] ships =
        [
            Ship("Match", id: "1", cargo: 100, price: 2_000_000, pledgePrice: 100),
            Ship("Too Small", id: "2", cargo: 10, price: 2_000_000, pledgePrice: 100),
            Ship("No Price", id: "3", cargo: 100, price: null, pledgePrice: 100)
        ];
        var filters = new ShipFilters(
            MinScu: 50,
            MinPrice: 1_000_000,
            MaxPrice: 3_000_000,
            PriceIsAuec: true,
            ShowFavoritesOnly: true,
            ShowFleetOnly: true);

        IReadOnlyList<FleetYardsShip> results = ShipCatalogCalculator.Apply(
            ships,
            string.Empty,
            filters,
            new HashSet<string>(["1", "2"]),
            new HashSet<string>(["1", "3"]));

        Assert.HasCount(1, results);
        Assert.AreEqual("Match", results[0].Name);
    }

    [TestMethod]
    public void FavoriteAndFleetFilters_UseSlugFallbackWhenIdIsMissing()
    {
        FleetYardsShip[] ships =
        [
            new() { Id = null, Slug = "slug-only", Name = "Slug Ship" },
            new() { Id = null, Slug = "other", Name = "Other Ship" }
        ];
        var filters = new ShipFilters(ShowFavoritesOnly: true, ShowFleetOnly: true);

        IReadOnlyList<FleetYardsShip> results = ShipCatalogCalculator.Apply(
            ships,
            string.Empty,
            filters,
            new HashSet<string>(["slug-only"]),
            new HashSet<string>(["slug-only"]));

        Assert.HasCount(1, results);
        Assert.AreEqual("Slug Ship", results[0].Name);
    }

    [TestMethod]
    [DataRow("NaN")]
    [DataRow("Infinity")]
    [DataRow("-Infinity")]
    [DataRow("not-a-number")]
    public void NumericInput_RejectsNonFiniteAndInvalidValues(string value)
    {
        Assert.IsFalse(ShipFilterInput.TryParseOptionalFinite(value, out _));
    }

    [TestMethod]
    public void NumericInput_AcceptsBlankAndFiniteValuesAndValidatesRanges()
    {
        Assert.IsTrue(ShipFilterInput.TryParseOptionalFinite(string.Empty, out double? blank));
        Assert.IsNull(blank);
        Assert.IsTrue(ShipFilterInput.TryParseOptionalFinite("125.5", out double? number));
        Assert.AreEqual(125.5, number);
        Assert.IsTrue(ShipFilterInput.IsOrdered(10, 10));
        Assert.IsFalse(ShipFilterInput.IsOrdered(11, 10));
    }

    private static FleetYardsShip Ship(
        string name,
        string? id = null,
        string? manufacturer = null,
        string? focus = null,
        string? classification = null,
        string? status = null,
        double? cargo = null,
        double? price = null,
        double? pledgePrice = null) =>
        new()
        {
            Id = id ?? name,
            Name = name,
            Focus = focus,
            ClassificationLabel = classification,
            ProductionStatus = status,
            Price = price,
            PledgePrice = pledgePrice,
            Manufacturer = manufacturer is null ? null : new FleetYardsManufacturer { Name = manufacturer },
            Metrics = new FleetYardsMetrics { Cargo = cargo }
        };
}
