using SCCompanion.Data.Wikelo;

namespace SCCompanion.Models;

public sealed class WikeloTradeCardItem : BindableObject
{
    private bool _isFavorite;

    public WikeloTradeCardItem(WikeloTrade trade, bool isFavorite)
    {
        Trade = trade;
        _isFavorite = isFavorite;
    }

    public WikeloTrade Trade { get; }

    public string MissionName => Trade.MissionName;

    public string RewardText => string.IsNullOrWhiteSpace(Trade.RewardName)
        ? "Reward unavailable"
        : Trade.RewardName;

    public string RequiredReputationText => string.IsNullOrWhiteSpace(Trade.RequiredReputation)
        ? "Required Reputation: Not specified"
        : $"Required Reputation: {Trade.RequiredReputation}";

    public bool IsFavorite
    {
        get => _isFavorite;
        set
        {
            if (_isFavorite == value)
            {
                return;
            }

            _isFavorite = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(FavoriteGlyph));
            OnPropertyChanged(nameof(FavoriteColor));
        }
    }

    public string FavoriteGlyph => IsFavorite ? "★" : "☆";

    public Color FavoriteColor => IsFavorite
        ? Color.FromArgb("#FFD700")
        : Color.FromArgb("#A0A0A0");
}

public sealed class WikeloProgressViewItem
{
    public WikeloProgressViewItem(WikeloTradeProgress progress)
    {
        Progress = progress;
    }

    public WikeloTradeProgress Progress { get; }

    public WikeloTrade Trade => Progress.Trade;

    public string MissionName => Trade.MissionName;

    public string PercentageText => $"{Progress.Percentage}%";

    public string QuantityText => $"{Progress.TotalOwned} of {Progress.TotalRequired} items";

    public double Fraction => Progress.Fraction;

    public Color ProgressColor => Progress.IsComplete
        ? Color.FromArgb("#129600")
        : Colors.White;
}

public sealed class WikeloInventoryViewItem
{
    public WikeloInventoryViewItem(WikeloInventoryItem inventoryItem)
    {
        InventoryItem = inventoryItem;
    }

    public WikeloInventoryItem InventoryItem { get; }

    public string DisplayName => InventoryItem.DisplayName;

    public string QuantityText => InventoryItem.OwnedQuantity.ToString();
}

public sealed class WikeloRequiredItemViewItem : BindableObject
{
    private int _ownedQuantity;

    public WikeloRequiredItemViewItem(WikeloItemProgress progress)
    {
        Item = progress.Item;
        _ownedQuantity = progress.ClampedOwnedQuantity;
    }

    public WikeloRequiredItem Item { get; }

    public string Name => Item.Name;

    public string RequiredQuantityText => $"Quantity: {Item.RequiredQuantity}";

    public int OwnedQuantity
    {
        get => _ownedQuantity;
        set
        {
            int clamped = Math.Clamp(value, 0, Item.RequiredQuantity);
            if (_ownedQuantity == clamped)
            {
                return;
            }

            _ownedQuantity = clamped;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsComplete));
            OnPropertyChanged(nameof(CompleteButtonText));
            OnPropertyChanged(nameof(CompleteButtonColor));
        }
    }

    public bool IsComplete => OwnedQuantity >= Item.RequiredQuantity;

    public string CompleteButtonText => IsComplete ? "Undo" : "☑";

    public Color CompleteButtonColor => IsComplete
        ? Color.FromArgb("#7A3434")
        : Color.FromArgb("#404040");
}
