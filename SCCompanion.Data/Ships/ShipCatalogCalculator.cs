namespace SCCompanion.Data.Ships;

public sealed record ShipFilters(
    IReadOnlySet<string>? Manufacturers = null,
    IReadOnlySet<string>? Classifications = null,
    IReadOnlySet<string>? Roles = null,
    IReadOnlySet<string>? ProductionStatuses = null,
    double? MinScu = null,
    double? MaxScu = null,
    double? MinPrice = null,
    double? MaxPrice = null,
    bool PriceIsAuec = false,
    bool ShowFavoritesOnly = false,
    bool ShowFleetOnly = false)
{
    public bool IsActive =>
        Manufacturers?.Count > 0 || Classifications?.Count > 0 ||
        Roles?.Count > 0 || ProductionStatuses?.Count > 0 ||
        MinScu is not null || MaxScu is not null || MinPrice is not null ||
        MaxPrice is not null || ShowFavoritesOnly || ShowFleetOnly;
}

public static class ShipCatalogCalculator
{
    public static readonly IReadOnlyList<string> RoleBuckets =
    [
        "Combat", "Freight", "Exploration", "Industrial", "Racing",
        "Touring / Luxury", "Medical", "Dropship", "Multi / Other", "Other"
    ];

    private static readonly IReadOnlyDictionary<string, IReadOnlySet<string>> RoleKeywords =
        new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal)
        {
            ["Combat"] = Set("fighter", "bomber", "gunship", "interdiction", "interdictor", "military", "assault", "destroyer", "frigate", "corvette", "carrier", "patrol", "boarding", "minelayer", "combat", "anti-air"),
            ["Freight"] = Set("freight", "cargo", "transport", "hauler", "freighter", "generalist", "transporter", "cargo loader"),
            ["Exploration"] = Set("exploration", "pathfinder", "expedition", "recon", "reconnaissance"),
            ["Industrial"] = Set("mining", "salvage", "repair", "refueling", "refinery", "construction", "recovery", "science", "prospecting"),
            ["Racing"] = Set("racing"),
            ["Touring / Luxury"] = Set("touring", "luxury", "passenger"),
            ["Medical"] = Set("medical", "ambulance"),
            ["Dropship"] = Set("dropship"),
            ["Multi / Other"] = Set("starter", "multi-role", "modular", "reporting", "data")
        };

    private static readonly IReadOnlySet<string> AllRoleKeywords =
        new HashSet<string>(RoleKeywords.Values.SelectMany(value => value), StringComparer.Ordinal);
    private static readonly IReadOnlySet<string> GroundLabels =
        Set("vehicle", "ground", "ground-vehicle", "ground vehicle");

    public static IReadOnlyList<FleetYardsShip> Apply(
        IEnumerable<FleetYardsShip> ships,
        string? query,
        ShipFilters filters,
        IReadOnlySet<string> favoriteIds,
        IReadOnlySet<string> fleetIds)
    {
        ArgumentNullException.ThrowIfNull(ships);
        ArgumentNullException.ThrowIfNull(filters);
        ArgumentNullException.ThrowIfNull(favoriteIds);
        ArgumentNullException.ThrowIfNull(fleetIds);

        IEnumerable<FleetYardsShip> filtered = ships;
        string normalizedQuery = query?.Trim() ?? string.Empty;
        if (normalizedQuery.Length > 0)
        {
            FleetYardsShip[] candidates = filtered
                .Where(ship => ship.Name?.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase) == true)
                .ToArray();
            filtered = candidates
                .Where(ship => ship.Name?.StartsWith(normalizedQuery, StringComparison.OrdinalIgnoreCase) == true)
                .Concat(candidates.Where(ship => ship.Name?.StartsWith(normalizedQuery, StringComparison.OrdinalIgnoreCase) != true));
        }

        if (filters.Manufacturers?.Count > 0)
            filtered = filtered.Where(ship => ship.Manufacturer?.Name is { } name && filters.Manufacturers.Contains(name));
        if (filters.Classifications?.Count > 0)
            filtered = FilterByClassification(filtered, filters.Classifications);
        if (filters.Roles?.Count > 0)
            filtered = FilterByRole(filtered, filters.Roles);
        if (filters.ProductionStatuses?.Count > 0)
            filtered = filtered.Where(ship => ship.ProductionStatus is { } status && filters.ProductionStatuses.Contains(status));
        if (filters.MinScu is { } minScu)
            filtered = filtered.Where(ship => (ship.Metrics?.Cargo ?? 0) >= minScu);
        if (filters.MaxScu is { } maxScu)
            filtered = filtered.Where(ship => (ship.Metrics?.Cargo ?? 0) <= maxScu);
        if (filters.MinPrice is not null || filters.MaxPrice is not null)
        {
            filtered = filtered.Where(ship =>
            {
                double? price = filters.PriceIsAuec ? ship.Price : ship.PledgePrice;
                return price is not null &&
                       (filters.MinPrice is null || price >= filters.MinPrice) &&
                       (filters.MaxPrice is null || price <= filters.MaxPrice);
            });
        }
        if (filters.ShowFavoritesOnly)
            filtered = filtered.Where(ship => favoriteIds.Contains(ShipIdentity.GetKey(ship)));
        if (filters.ShowFleetOnly)
            filtered = filtered.Where(ship => fleetIds.Contains(ShipIdentity.GetKey(ship)));

        return filtered.ToArray();
    }

    private static IEnumerable<FleetYardsShip> FilterByClassification(
        IEnumerable<FleetYardsShip> ships,
        IReadOnlySet<string> selected)
    {
        bool wantGround = selected.Contains("ground_vehicle");
        bool wantShip = selected.Contains("ship");
        if (wantGround == wantShip) return ships;
        return ships.Where(ship =>
        {
            bool isGround = ship.ClassificationLabel is { } label &&
                            GroundLabels.Contains(label.ToLowerInvariant());
            return wantGround ? isGround : !isGround;
        });
    }

    private static IEnumerable<FleetYardsShip> FilterByRole(
        IEnumerable<FleetYardsShip> ships,
        IReadOnlySet<string> selected)
    {
        bool wantOther = selected.Contains("Other");
        return ships.Where(ship =>
        {
            if (ship.Focus is not { } focusValue) return false;
            string focus = focusValue.ToLowerInvariant();
            foreach (string label in selected.Where(label => label != "Other"))
            {
                if (RoleKeywords.TryGetValue(label, out IReadOnlySet<string>? keywords) &&
                    keywords.Any(focus.Contains))
                    return true;
            }
            return wantOther && !AllRoleKeywords.Any(focus.Contains);
        });
    }

    private static IReadOnlySet<string> Set(params string[] values) =>
        new HashSet<string>(values, StringComparer.Ordinal);
}
