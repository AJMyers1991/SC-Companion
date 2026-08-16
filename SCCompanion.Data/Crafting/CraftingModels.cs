using System.Text.Json.Serialization;

namespace SCCompanion.Data.Crafting;

public sealed record CraftingBlueprintsResponse(
    [property: JsonPropertyName("items")] IReadOnlyList<CraftingBlueprint>? Items,
    [property: JsonPropertyName("pagination")] CraftingPagination? Pagination);

public sealed record CraftingBlueprint(
    [property: JsonPropertyName("id")] long? Id,
    [property: JsonPropertyName("blueprint_id")] string? BlueprintId,
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("loc_key")] string? LocalizationKey,
    [property: JsonPropertyName("category")] string? Category,
    [property: JsonPropertyName("craft_time_seconds")] int? CraftTimeSeconds,
    [property: JsonPropertyName("tiers")] int? Tiers,
    [property: JsonPropertyName("ingredients")] IReadOnlyList<CraftingIngredient>? Ingredients,
    [property: JsonPropertyName("missions")] IReadOnlyList<CraftingMission>? Missions,
    [property: JsonPropertyName("item_stats")] CraftingItemStats? ItemStats);

public sealed record CraftingIngredient(
    [property: JsonPropertyName("slot")] string? Slot,
    [property: JsonPropertyName("slot_loc_key")] string? SlotLocalizationKey,
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("quantity_scu")] double? QuantityScu,
    [property: JsonPropertyName("options")] IReadOnlyList<CraftingIngredientOption>? Options,
    [property: JsonPropertyName("quality_effects")] IReadOnlyList<CraftingQualityEffect>? QualityEffects);

public sealed record CraftingIngredientOption(
    [property: JsonPropertyName("guid")] string? Guid,
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("quantity_scu")] double? QuantityScu,
    [property: JsonPropertyName("min_quality")] int? MinimumQuality,
    [property: JsonPropertyName("unit")] string? Unit);

public sealed record CraftingQualityEffect(
    [property: JsonPropertyName("stat")] string? Stat,
    [property: JsonPropertyName("stat_loc_key")] string? StatLocalizationKey,
    [property: JsonPropertyName("quality_min")] int? QualityMinimum,
    [property: JsonPropertyName("quality_max")] int? QualityMaximum,
    [property: JsonPropertyName("modifier_at_min")] double? ModifierAtMinimum,
    [property: JsonPropertyName("modifier_at_max")] double? ModifierAtMaximum,
    [property: JsonPropertyName("type")] string? Type);

public sealed record CraftingMission(
    [property: JsonPropertyName("mission_id")] long? MissionId,
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("contractor")] string? Contractor,
    [property: JsonPropertyName("drop_chance")] string? DropChance,
    [property: JsonPropertyName("pool_size")] int? PoolSize,
    [property: JsonPropertyName("locations")] string? Locations);

public sealed record CraftingItemStats(
    [property: JsonPropertyName("type")] string? Type,
    [property: JsonPropertyName("mass_kg")] double? MassKg,
    [property: JsonPropertyName("fire_modes")] IReadOnlyList<CraftingFireMode>? FireModes,
    [property: JsonPropertyName("max_ammo")] int? MaximumAmmo,
    [property: JsonPropertyName("overheat_temperature")] double? OverheatTemperature);

public sealed record CraftingFireMode(
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("fire_rate")] int? FireRate,
    [property: JsonPropertyName("heat_per_shot")] double? HeatPerShot,
    [property: JsonPropertyName("wear_per_shot")] double? WearPerShot,
    [property: JsonPropertyName("ammo_cost")] int? AmmoCost,
    [property: JsonPropertyName("pellet_count")] int? PelletCount,
    [property: JsonPropertyName("damage_multiplier")] double? DamageMultiplier,
    [property: JsonPropertyName("spread")] CraftingSpread? Spread);

public sealed record CraftingSpread(
    [property: JsonPropertyName("min")] double? Minimum,
    [property: JsonPropertyName("max")] double? Maximum,
    [property: JsonPropertyName("first_attack")] double? FirstAttack,
    [property: JsonPropertyName("attack")] double? Attack,
    [property: JsonPropertyName("decay")] double? Decay);

public sealed record CraftingPagination(
    [property: JsonPropertyName("page")] int? Page,
    [property: JsonPropertyName("limit")] int? Limit,
    [property: JsonPropertyName("total")] int? Total,
    [property: JsonPropertyName("pages")] int? Pages);
