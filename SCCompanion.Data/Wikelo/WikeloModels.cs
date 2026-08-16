namespace SCCompanion.Data.Wikelo;

/// <summary>
/// One item and quantity required by a Wikelo trade mission.
/// </summary>
public sealed record WikeloRequiredItem(
    string Id,
    string Name,
    int RequiredQuantity,
    int SourceOrder);

/// <summary>
/// A trade mission loaded from the Wikelo Trades data set.
/// </summary>
public sealed record WikeloTrade(
    string Id,
    string MissionName,
    string RewardName,
    string Category,
    string Patch,
    string RequiredReputation,
    IReadOnlyList<WikeloRequiredItem> RequiredItems,
    string Description,
    bool IsActive);

/// <summary>
/// The persisted quantity and derived state for one required item.
/// </summary>
public sealed record WikeloItemProgress(
    WikeloRequiredItem Item,
    int OwnedQuantity)
{
    public int ClampedOwnedQuantity => Math.Clamp(OwnedQuantity, 0, Item.RequiredQuantity);

    public bool IsUntouched => ClampedOwnedQuantity == 0;

    public bool IsComplete => ClampedOwnedQuantity >= Item.RequiredQuantity;

    public bool IsPartial => !IsUntouched && !IsComplete;
}

/// <summary>
/// Weighted completion state for one trade mission.
/// </summary>
public sealed record WikeloTradeProgress(
    WikeloTrade Trade,
    IReadOnlyList<WikeloItemProgress> Items,
    int TotalRequired,
    int TotalOwned,
    double Fraction)
{
    public int Percentage => (int)Math.Floor(Fraction * 100d);

    public bool IsComplete => TotalRequired > 0 && TotalOwned >= TotalRequired;
}

/// <summary>
/// Aggregated inventory quantity for one required item name.
/// </summary>
public sealed record WikeloInventoryItem(
    string IngredientId,
    string DisplayName,
    int OwnedQuantity);
