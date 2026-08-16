using System.Collections.ObjectModel;
using SCCompanion.Data;
using SCCompanion.Data.Caching;
using SCCompanion.Data.Entities;
using SCCompanion.Data.Wikelo;
using SCCompanion.Models;

namespace SCCompanion.Views;

public partial class WikeloDetailPage : ContentPage
{
    private const string FavoriteCategory = "wikelo-trade";

    private readonly WikeloTrade _trade;
    private readonly AppDatabase _database;
    private readonly WikeloRewardImageService _rewardImageService;
    private readonly DiskResourceCache _resourceCache;
    private readonly SemaphoreSlim _quantitySaveLock = new(1, 1);
    private CancellationTokenSource? _pageCancellation;
    private bool _favoriteEnsured;
    private double _progressFraction;
    private string _progressPercentageText = "0% Complete";
    private string _progressQuantityText = "0 of 0 items";
    private Color _progressColor = Colors.White;
    private string _rewardImageSource = "icon_wikelo.png";
    private bool _isImageLoading;
    private bool _isFavorite;

    public WikeloDetailPage(
        WikeloTrade trade,
        AppDatabase database,
        WikeloRewardImageService rewardImageService,
        DiskResourceCache resourceCache)
    {
        _trade = trade;
        _database = database;
        _rewardImageService = rewardImageService;
        _resourceCache = resourceCache;

        InitializeComponent();
        BindingContext = this;

        foreach (string reward in SplitRewards(trade.RewardName))
        {
            Rewards.Add(reward);
        }
    }

    public string TradeTitle => _trade.MissionName;

    public string RequiredReputationText => string.IsNullOrWhiteSpace(_trade.RequiredReputation)
        ? "Required Reputation: Not specified"
        : $"Required Reputation: {_trade.RequiredReputation}";

    public string CategoryText => string.IsNullOrWhiteSpace(_trade.Category)
        ? "Other"
        : _trade.Category;

    public ObservableCollection<string> Rewards { get; } = [];

    public ObservableCollection<WikeloRequiredItemViewItem> RequiredItems { get; } = [];

    public string FavoriteGlyph => IsFavorite ? "★" : "☆";

    public Color FavoriteColor => IsFavorite
        ? Color.FromArgb("#FFD700")
        : Color.FromArgb("#A0A0A0");

    public bool IsFavorite
    {
        get => _isFavorite;
        private set
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

    public double ProgressFraction
    {
        get => _progressFraction;
        private set
        {
            if (Math.Abs(_progressFraction - value) < 0.0001d)
            {
                return;
            }

            _progressFraction = value;
            OnPropertyChanged();
        }
    }

    public string ProgressPercentageText
    {
        get => _progressPercentageText;
        private set
        {
            if (_progressPercentageText == value)
            {
                return;
            }

            _progressPercentageText = value;
            OnPropertyChanged();
        }
    }

    public string ProgressQuantityText
    {
        get => _progressQuantityText;
        private set
        {
            if (_progressQuantityText == value)
            {
                return;
            }

            _progressQuantityText = value;
            OnPropertyChanged();
        }
    }

    public Color ProgressColor
    {
        get => _progressColor;
        private set
        {
            if (_progressColor == value)
            {
                return;
            }

            _progressColor = value;
            OnPropertyChanged();
        }
    }

    public string RewardImageSource
    {
        get => _rewardImageSource;
        private set
        {
            if (_rewardImageSource == value)
            {
                return;
            }

            _rewardImageSource = value;
            OnPropertyChanged();
        }
    }

    public bool IsImageLoading
    {
        get => _isImageLoading;
        private set
        {
            if (_isImageLoading == value)
            {
                return;
            }

            _isImageLoading = value;
            OnPropertyChanged();
        }
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        _pageCancellation?.Cancel();
        _pageCancellation?.Dispose();
        _pageCancellation = new CancellationTokenSource();

        await LoadFavoriteAsync();
        await LoadProgressAsync();
        await LoadRewardImageAsync(_pageCancellation.Token);
    }

    private async Task LoadFavoriteAsync()
    {
        IReadOnlyList<FavoriteRecord> favorites =
            await _database.GetFavoritesAsync(FavoriteCategory);
        IsFavorite = favorites.Any(favorite =>
            string.Equals(
                favorite.ExternalId,
                _trade.Id,
                StringComparison.OrdinalIgnoreCase));
        _favoriteEnsured = IsFavorite;
    }

    private async void OnFavoriteToggled(object? sender, EventArgs e)
    {
        if (sender is Button button)
        {
            button.IsEnabled = false;
        }

        try
        {
            IsFavorite = await _database.ToggleFavoriteAsync(
                FavoriteCategory,
                _trade.Id,
                _trade.MissionName);
            _favoriteEnsured = IsFavorite;

            if (!IsFavorite)
            {
                await _database.DeleteWikeloTradeProgressAsync(_trade.Id);
                foreach (WikeloRequiredItemViewItem item in RequiredItems)
                {
                    item.OwnedQuantity = 0;
                }

                RefreshProgressFromVisibleItems();
            }
        }
        catch (Exception exception)
        {
            System.Diagnostics.Debug.WriteLine($"Unable to update Wikelo favorite: {exception}");
            await DisplayAlertAsync(
                "Favorite Not Saved",
                "SC Companion could not update that favorite.",
                "OK");
        }
        finally
        {
            if (sender is Button favoriteButton)
            {
                favoriteButton.IsEnabled = true;
            }
        }
    }

    protected override void OnDisappearing()
    {
        _pageCancellation?.Cancel();
        _pageCancellation?.Dispose();
        _pageCancellation = null;
        base.OnDisappearing();
    }

    private async Task LoadProgressAsync()
    {
        try
        {
            IReadOnlyList<WikeloTradeProgressRecord> records =
                await _database.GetWikeloTradeProgressAsync(_trade.Id);
            WikeloTradeProgress progress = WikeloProgressCalculator.Calculate(
                _trade,
                records);

            ProgressFraction = progress.Fraction;
            ProgressPercentageText = $"{progress.Percentage}% Complete";
            ProgressQuantityText = $"{progress.TotalOwned} of {progress.TotalRequired} items";
            ProgressColor = progress.IsComplete
                ? Color.FromArgb("#129600")
                : Colors.White;

            RequiredItems.Clear();
            foreach (WikeloItemProgress item in
                     WikeloProgressCalculator.OrderRequiredItems(progress))
            {
                RequiredItems.Add(new WikeloRequiredItemViewItem(item));
            }
        }
        catch (Exception exception)
        {
            System.Diagnostics.Debug.WriteLine($"Unable to load Wikelo trade progress: {exception}");
            await DisplayAlertAsync(
                "Progress Unavailable",
                "SC Companion could not load progress for this trade.",
                "OK");
        }
    }

    private async Task LoadRewardImageAsync(CancellationToken cancellationToken)
    {
        string? cachedImagePath = _resourceCache.TryGetCachedPath(
            "wikelo-rewards",
            _trade.Id);
        if (!string.IsNullOrWhiteSpace(cachedImagePath))
        {
            RewardImageSource = cachedImagePath;
            return;
        }

        if (string.IsNullOrWhiteSpace(_trade.RewardName))
        {
            RewardImageSource = "icon_wikelo.png";
            return;
        }

        IsImageLoading = true;
        try
        {
            string firstReward = SplitRewards(_trade.RewardName).FirstOrDefault() ??
                _trade.RewardName;
            string? imageUrl = await _rewardImageService.FindRewardImageAsync(
                firstReward,
                cancellationToken);
            if (!cancellationToken.IsCancellationRequested)
            {
                RewardImageSource = string.IsNullOrWhiteSpace(imageUrl)
                    ? "icon_wikelo.png"
                    : await _resourceCache.GetOrDownloadAsync(
                        new Uri(imageUrl),
                        "wikelo-rewards",
                        _trade.Id,
                        cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            System.Diagnostics.Debug.WriteLine($"Unable to load Wikelo reward image: {exception}");
            RewardImageSource = "icon_wikelo.png";
        }
        finally
        {
            if (!cancellationToken.IsCancellationRequested)
            {
                IsImageLoading = false;
            }
        }
    }

    private async void OnCompleteResetClicked(object? sender, EventArgs e)
    {
        if (sender is not Button { CommandParameter: WikeloRequiredItemViewItem item })
        {
            return;
        }

        int quantity = item.IsComplete ? 0 : item.Item.RequiredQuantity;
        await SetOwnedQuantityAsync(item, quantity);
    }

    private async void OnQuantityDecrement(object? sender, EventArgs e)
    {
        if (sender is Button { CommandParameter: WikeloRequiredItemViewItem item })
        {
            await SetOwnedQuantityAsync(item, item.OwnedQuantity - 1);
        }
    }

    private async void OnQuantityIncrement(object? sender, EventArgs e)
    {
        if (sender is Button { CommandParameter: WikeloRequiredItemViewItem item })
        {
            await SetOwnedQuantityAsync(item, item.OwnedQuantity + 1);
        }
    }

    private async void OnQuantityEntryCompleted(object? sender, EventArgs e)
    {
        await ApplyQuantityEntryAsync(sender as Entry);
    }

    private async void OnQuantityEntryUnfocused(object? sender, FocusEventArgs e)
    {
        await ApplyQuantityEntryAsync(sender as Entry);
    }

    private async Task ApplyQuantityEntryAsync(Entry? entry)
    {
        if (entry?.BindingContext is not WikeloRequiredItemViewItem item)
        {
            return;
        }

        if (!int.TryParse(entry.Text, out int quantity))
        {
            entry.Text = item.OwnedQuantity.ToString();
            return;
        }

        await SetOwnedQuantityAsync(item, quantity);
    }

    private async Task SetOwnedQuantityAsync(
        WikeloRequiredItemViewItem item,
        int quantity)
    {
        int clampedQuantity = Math.Clamp(quantity, 0, item.Item.RequiredQuantity);
        int previousQuantity = item.OwnedQuantity;
        if (clampedQuantity == previousQuantity)
        {
            return;
        }

        // Update in place. Rebuilding every required-item Entry after each tap caused
        // Unfocused events to recursively start the same save/reload path.
        item.OwnedQuantity = clampedQuantity;
        RefreshProgressFromVisibleItems();

        try
        {
            await _quantitySaveLock.WaitAsync();
            try
            {
                await _database.SetWikeloTradeProgressAsync(
                    _trade.Id,
                    item.Item.Id,
                    clampedQuantity);

                if (clampedQuantity > 0 && !_favoriteEnsured)
                {
                    await _database.SaveFavoriteAsync(new FavoriteRecord
                    {
                        Category = FavoriteCategory,
                        ExternalId = _trade.Id,
                        DisplayName = _trade.MissionName
                    });
                    _favoriteEnsured = true;
                }
            }
            finally
            {
                _quantitySaveLock.Release();
            }
        }
        catch (Exception exception)
        {
            item.OwnedQuantity = previousQuantity;
            RefreshProgressFromVisibleItems();
            System.Diagnostics.Debug.WriteLine($"Unable to save Wikelo quantity: {exception}");
            await DisplayAlertAsync(
                "Progress Not Saved",
                "SC Companion could not save that item quantity.",
                "OK");
        }
    }

    private void RefreshProgressFromVisibleItems()
    {
        int totalRequired = RequiredItems.Sum(item => item.Item.RequiredQuantity);
        int totalOwned = RequiredItems.Sum(item => item.OwnedQuantity);
        ProgressFraction = totalRequired == 0
            ? 0d
            : (double)totalOwned / totalRequired;
        ProgressPercentageText = $"{Math.Round(ProgressFraction * 100d)}% Complete";
        ProgressQuantityText = $"{totalOwned} of {totalRequired} items";
        ProgressColor = totalRequired > 0 && totalOwned >= totalRequired
            ? Color.FromArgb("#129600")
            : Colors.White;

        WikeloRequiredItemViewItem[] orderedItems = RequiredItems
            .OrderBy(GetCompletionGroup)
            .ThenByDescending(item => item.OwnedQuantity)
            .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        for (int targetIndex = 0; targetIndex < orderedItems.Length; targetIndex++)
        {
            int currentIndex = RequiredItems.IndexOf(orderedItems[targetIndex]);
            if (currentIndex != targetIndex)
            {
                RequiredItems.Move(currentIndex, targetIndex);
            }
        }
    }

    private static int GetCompletionGroup(WikeloRequiredItemViewItem item)
    {
        if (item.IsComplete)
        {
            return 2;
        }

        return item.OwnedQuantity > 0 ? 0 : 1;
    }

    private void OnQuantityEntryHandlerChanged(object? sender, EventArgs e)
    {
#if ANDROID
        if (sender is Entry entry &&
            entry.Handler?.PlatformView is AndroidX.AppCompat.Widget.AppCompatEditText nativeEntry)
        {
            nativeEntry.BackgroundTintList =
                Android.Content.Res.ColorStateList.ValueOf(Android.Graphics.Color.Transparent);
        }
#endif
    }

    private static IReadOnlyList<string> SplitRewards(string rewardName)
    {
        return rewardName
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(reward => reward.Length > 0)
            .ToArray();
    }
}
