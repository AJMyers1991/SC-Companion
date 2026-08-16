using System.Text.Json.Serialization;

namespace SCCompanion.Data.Ships;

public sealed class FleetYardsModelsResponse
{
    [JsonPropertyName("items")]
    public List<FleetYardsShip>? Items { get; init; }

    [JsonPropertyName("meta")]
    public FleetYardsMeta? Meta { get; init; }
}

public sealed class FleetYardsMeta
{
    [JsonPropertyName("pagination")]
    public FleetYardsPagination? Pagination { get; init; }
}

public sealed class FleetYardsPagination
{
    [JsonPropertyName("totalPages")]
    public int? TotalPages { get; init; }
}

public sealed class FleetYardsShip
{
    public string? Id { get; init; }
    public string? Name { get; init; }
    public string? Slug { get; init; }
    public string? Description { get; init; }
    public string? Focus { get; init; }
    public string? Classification { get; init; }
    public string? ClassificationLabel { get; init; }
    public string? ProductionStatus { get; init; }
    public string? ScIdentifier { get; init; }
    public bool? InGame { get; init; }
    public bool? OnSale { get; init; }
    public FleetYardsManufacturer? Manufacturer { get; init; }
    public FleetYardsCrew? Crew { get; init; }
    public FleetYardsMetrics? Metrics { get; init; }
    public FleetYardsSpeeds? Speeds { get; init; }
    public FleetYardsShipMedia? Media { get; init; }
    public FleetYardsLinks? Links { get; init; }
    public double? Price { get; init; }
    public string? PriceLabel { get; init; }
    public double? PledgePrice { get; init; }
    public string? PledgePriceLabel { get; init; }
    public List<FleetYardsLoaner>? Loaners { get; init; }
    public string? Brochure { get; init; }
}

public sealed class FleetYardsManufacturer
{
    public string? Name { get; init; }
    public string? LongName { get; init; }
    public string? Slug { get; init; }
    public string? Code { get; init; }
    public FleetYardsImage? Logo { get; init; }
}

public sealed class FleetYardsCrew
{
    public int? Min { get; init; }
    public int? Max { get; init; }
    public string? MinLabel { get; init; }
    public string? MaxLabel { get; init; }
}

public sealed class FleetYardsMetrics
{
    public double? Beam { get; init; }
    public string? BeamLabel { get; init; }
    public double? Height { get; init; }
    public string? HeightLabel { get; init; }
    public double? Length { get; init; }
    public string? LengthLabel { get; init; }
    public double? Mass { get; init; }
    public string? MassLabel { get; init; }
    public string? Size { get; init; }
    public string? SizeLabel { get; init; }
    public double? Cargo { get; init; }
    public string? CargoLabel { get; init; }
    public double? HydrogenFuelTankSize { get; init; }
    public double? QuantumFuelTankSize { get; init; }
    public bool? IsGroundVehicle { get; init; }
}

public sealed class FleetYardsSpeeds
{
    public double? ScmSpeed { get; init; }
    public double? ScmSpeedBoosted { get; init; }
    public double? PitchBoosted { get; init; }
    public double? YawBoosted { get; init; }
    public double? RollBoosted { get; init; }
}

public sealed class FleetYardsShipMedia
{
    public FleetYardsImage? StoreImage { get; init; }
    public FleetYardsImage? FrontView { get; init; }
    public FleetYardsImage? SideView { get; init; }
    public FleetYardsImage? TopView { get; init; }
    public FleetYardsImage? AngledView { get; init; }
    public FleetYardsImage? AngledViewColored { get; init; }
    public FleetYardsImage? FrontViewColored { get; init; }
    public FleetYardsImage? SideViewColored { get; init; }
    public FleetYardsImage? TopViewColored { get; init; }
}

public sealed class FleetYardsImage
{
    public string? Name { get; init; }
    public string? Url { get; init; }
    public string? SmallUrl { get; init; }
    public string? MediumUrl { get; init; }
    public string? LargeUrl { get; init; }
    public string? XlargeUrl { get; init; }

    public string? BestListUrl => MediumUrl ?? SmallUrl ?? Url;
    public string? BestDetailUrl => XlargeUrl ?? LargeUrl ?? MediumUrl ?? Url;
}

public sealed class FleetYardsLinks
{
    public string? StoreUrl { get; init; }
    public string? SalesPageUrl { get; init; }
    public string? Self { get; init; }
    public string? Frontend { get; init; }
}

public sealed class FleetYardsLoaner
{
    public string? Name { get; init; }
    public string? Slug { get; init; }
}

public sealed class FleetYardsHardpoint
{
    public string? Id { get; init; }
    public string? Name { get; init; }
    public string? GroupKey { get; init; }
    public string? Group { get; init; }
    public string? Category { get; init; }
    public int? MinSize { get; init; }
    public int? MaxSize { get; init; }
    public List<FleetYardsHardpoint>? Hardpoints { get; init; }
}

public sealed class FleetYardsModule
{
    public string? Id { get; init; }
    public string? Name { get; init; }
    public string? Slug { get; init; }
    public string? Description { get; init; }
    public FleetYardsModuleMetrics? Metrics { get; init; }
    public FleetYardsModuleMedia? Media { get; init; }
}

public sealed class FleetYardsModuleMetrics { public double? Cargo { get; init; } }
public sealed class FleetYardsModuleMedia { public FleetYardsImage? StoreImage { get; init; } }

public sealed class FleetYardsPaint
{
    public string? Id { get; init; }
    public string? Name { get; init; }
    public string? Slug { get; init; }
    public string? Description { get; init; }
    public FleetYardsPaintMedia? Media { get; init; }
}

public sealed class FleetYardsPaintMedia { public FleetYardsImage? StoreImage { get; init; } }

public sealed record ShipDetailData(
    IReadOnlyList<FleetYardsHardpoint> Hardpoints,
    IReadOnlyList<FleetYardsModule> Modules,
    IReadOnlyList<FleetYardsPaint> Paints);
