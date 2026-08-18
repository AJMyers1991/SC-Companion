using System.Collections.ObjectModel;
using SCCompanion.Data;
using SCCompanion.Data.Caching;
using SCCompanion.Data.Entities;
using SCCompanion.Data.Wikelo;
using SCCompanion.Models;

namespace SCCompanion.Views;

public partial class WikeloPage : ContentPage
{
    private const string FavoriteCategory = "wikelo-trade";
    private static readonly Uri ProviderUri = new(
        "https://ruadhan2301.github.io/RecipeAvailabilityManager/");

    private readonly AppDatabase _database;
    private readonly WikeloTradeService _tradeService;
    private readonly WikeloRewardImageService _rewardImageService;
    private readonly DiskResourceCache _resourceCache;

    private IReadOnlyList<WikeloTrade> _allTrades = [];
    private HashSet<string> _favoriteIds = new(StringComparer.OrdinalIgnoreCase);
    private CancellationTokenSource? _pageCancellation;
    private bool _isLoading;
    private bool _isOpeningTrade;

    public WikeloPage(
        AppDatabase database,
        WikeloTradeService tradeService,
        WikeloRewardImageService rewardImageService,
        DiskResourceCache resourceCache)
    {
        _database = database;
        _tradeService = tradeService;
        _rewardImageService = rewardImageService;
        _resourceCache = resourceCache;

        InitializeComponent();
        BindingContext = this;
    }

    public ObservableCollection<WikeloTradeCardItem> Trades { get; } = [];

    public ObservableCollection<WikeloProgressViewItem> ProgressItems { get; } = [];

    public ObservableCollection<WikeloInventoryViewItem> InventoryItems { get; } = [];

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadPageAsync();
    }

    protected override void OnDisappearing()
    {
        _pageCancellation?.Cancel();
        _pageCancellation?.Dispose();
        _pageCancellation = null;
        base.OnDisappearing();
    }

    private async Task LoadPageAsync()
    {
        if (_isLoading)
        {
            return;
        }

        _isLoading = true;
        _pageCancellation?.Cancel();
        _pageCancellation?.Dispose();
        _pageCancellation = new CancellationTokenSource();
        CancellationToken cancellationToken = _pageCancellation.Token;

        SearchActivityIndicator.IsRunning = true;
        SearchActivityIndicator.IsVisible = true;
        ShowStatus("Loading Wikelo trades...");

        try
        {
            Task<IReadOnlyList<WikeloTrade>> tradesTask =
                _tradeService.LoadTradesAsync(cancellationToken);
            Task<IReadOnlyList<FavoriteRecord>> favoritesTask =
                _database.GetFavoritesAsync(FavoriteCategory);

            await Task.WhenAll(tradesTask, favoritesTask);
            cancellationToken.ThrowIfCancellationRequested();

            _allTrades = await tradesTask;
            _favoriteIds = (await favoritesTask)
                .Select(favorite => favorite.ExternalId)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            ApplySearch(SearchEntry.Text);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            System.Diagnostics.Debug.WriteLine($"Unable to load Wikelo trades: {exception}");
            ShowStatus("Wikelo trades are unavailable. Check your connection and try again.");
        }
        finally
        {
            if (!cancellationToken.IsCancellationRequested)
            {
                SearchActivityIndicator.IsRunning = false;
                SearchActivityIndicator.IsVisible = false;
            }

            _isLoading = false;
        }
    }

    private void OnSearchTextChanged(object? sender, TextChangedEventArgs e)
    {
        ApplySearch(e.NewTextValue);
    }

    private void ApplySearch(string? query)
    {
        IReadOnlyList<WikeloTrade> matches = WikeloTradeSearchEngine.Search(
            _allTrades,
            query);

        IEnumerable<WikeloTrade> sorted = matches
            .OrderBy(trade => !_favoriteIds.Contains(trade.Id))
            .ThenBy(trade => trade.MissionName, StringComparer.OrdinalIgnoreCase);

        Trades.Clear();
        foreach (WikeloTrade trade in sorted)
        {
            Trades.Add(new WikeloTradeCardItem(
                trade,
                _favoriteIds.Contains(trade.Id)));
        }

        if (_allTrades.Count == 0)
        {
            return;
        }

        if (Trades.Count == 0)
        {
            ShowStatus("No matching Wikelo trades found.");
        }
        else
        {
            ShowStatus($"{Trades.Count} {(Trades.Count == 1 ? "trade" : "trades")}");
        }
    }

    private async void OnFavoriteToggled(object? sender, EventArgs e)
    {
        if (sender is not Button { CommandParameter: WikeloTradeCardItem item })
        {
            return;
        }

        try
        {
            bool isFavorite = await _database.ToggleFavoriteAsync(
                FavoriteCategory,
                item.Trade.Id,
                item.Trade.MissionName);
            item.IsFavorite = isFavorite;

            if (isFavorite)
            {
                _favoriteIds.Add(item.Trade.Id);
            }
            else
            {
                _favoriteIds.Remove(item.Trade.Id);
                await _database.DeleteWikeloTradeProgressAsync(item.Trade.Id);
            }

            ApplySearch(SearchEntry.Text);
        }
        catch (Exception exception)
        {
            System.Diagnostics.Debug.WriteLine($"Unable to update Wikelo favorite: {exception}");
            await DisplayAlertAsync(
                "Favorite Not Saved",
                "SC Companion could not update that favorite.",
                "OK");
        }
    }

    private async void OnTradeSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (_isOpeningTrade ||
            e.CurrentSelection.FirstOrDefault() is not WikeloTradeCardItem item)
        {
            return;
        }

        TradesCollectionView.SelectedItem = null;
        await OpenTradeAsync(item.Trade);
    }

    private async void OnProgressTradeSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is not WikeloProgressViewItem item)
        {
            return;
        }

        ProgressCollectionView.SelectedItem = null;
        ProgressOverlay.IsVisible = false;
        await OpenTradeAsync(item.Trade);
    }

    private async Task OpenTradeAsync(WikeloTrade trade)
    {
        _isOpeningTrade = true;
        WikeloNavigationLoader.IsRunning = true;
        try
        {
            await Task.Yield();
            await Navigation.PushAsync(new WikeloDetailPage(
                trade,
                _database,
                _rewardImageService,
                _resourceCache));
        }
        catch (Exception exception)
        {
            System.Diagnostics.Debug.WriteLine($"Unable to open Wikelo trade: {exception}");
            await DisplayAlertAsync(
                "Unable to Open Trade",
                "SC Companion could not open that Wikelo trade.",
                "OK");
        }
        finally
        {
            WikeloNavigationLoader.IsRunning = false;
            _isOpeningTrade = false;
        }
    }

    private async void OnMyProgressClicked(object? sender, EventArgs e)
    {
        if (_favoriteIds.Count == 0)
        {
            await DisplayAlertAsync(
                "No Favorite Trades",
                "Favorite a Wikelo trade to begin tracking its progress.",
                "OK");
            return;
        }

        try
        {
            IReadOnlyList<WikeloTradeProgress> progress =
                await LoadFavoriteProgressAsync();
            ProgressItems.Clear();
            foreach (WikeloTradeProgress tradeProgress in progress
                         .OrderBy(item => item.Trade.MissionName, StringComparer.OrdinalIgnoreCase))
            {
                ProgressItems.Add(new WikeloProgressViewItem(tradeProgress));
            }

            ProgressOverlay.IsVisible = true;
        }
        catch (Exception exception)
        {
            System.Diagnostics.Debug.WriteLine($"Unable to load Wikelo progress: {exception}");
            await DisplayAlertAsync(
                "Progress Unavailable",
                "SC Companion could not load your Wikelo progress.",
                "OK");
        }
    }

    private async void OnMyInventoryClicked(object? sender, EventArgs e)
    {
        if (_favoriteIds.Count == 0)
        {
            await DisplayAlertAsync(
                "No Favorite Trades",
                "Favorite a Wikelo trade to begin tracking inventory.",
                "OK");
            return;
        }

        try
        {
            IReadOnlyList<WikeloTradeProgress> progress =
                await LoadFavoriteProgressAsync();
            IReadOnlyList<WikeloInventoryItem> inventory =
                WikeloProgressCalculator.AggregateInventory(progress);

            InventoryItems.Clear();
            foreach (WikeloInventoryItem item in inventory)
            {
                InventoryItems.Add(new WikeloInventoryViewItem(item));
            }

            InventoryEmptyLabel.IsVisible = InventoryItems.Count == 0;
            InventoryCollectionView.IsVisible = InventoryItems.Count > 0;
            InventoryOverlay.IsVisible = true;
        }
        catch (Exception exception)
        {
            System.Diagnostics.Debug.WriteLine($"Unable to load Wikelo inventory: {exception}");
            await DisplayAlertAsync(
                "Inventory Unavailable",
                "SC Companion could not load your Wikelo inventory.",
                "OK");
        }
    }

    private async Task<IReadOnlyList<WikeloTradeProgress>> LoadFavoriteProgressAsync()
    {
        IReadOnlyList<WikeloTradeProgressRecord> records =
            await _database.GetAllWikeloTradeProgressAsync();
        return _allTrades
            .Where(trade => _favoriteIds.Contains(trade.Id))
            .Select(trade => WikeloProgressCalculator.Calculate(trade, records))
            .ToArray();
    }

    private void OnCloseProgressModal(object? sender, EventArgs e)
    {
        ProgressOverlay.IsVisible = false;
    }

    private void OnCloseInventoryModal(object? sender, EventArgs e)
    {
        InventoryOverlay.IsVisible = false;
    }

    private async void OnProviderTapped(object? sender, TappedEventArgs e)
    {
        try
        {
            await Browser.Default.OpenAsync(ProviderUri, BrowserLaunchMode.SystemPreferred);
        }
        catch (Exception exception)
        {
            System.Diagnostics.Debug.WriteLine($"Unable to open Wikelo attribution link: {exception}");
            await DisplayAlertAsync(
                "Unable to Open Link",
                "SC Companion could not open the Wikelo data source.",
                "OK");
        }
    }

    private void OnSearchEntryHandlerChanged(object? sender, EventArgs e)
    {
#if ANDROID
        if (sender is Entry entry &&
            entry.Handler?.PlatformView is AndroidX.AppCompat.Widget.AppCompatEditText nativeEntry)
        {
            nativeEntry.BackgroundTintList =
                Android.Content.Res.ColorStateList.ValueOf(Android.Graphics.Color.Transparent);
        }
#endif
        NativeSearchInputChrome.RemoveIosChrome(sender);
    }

    private void ShowStatus(string message)
    {
        StatusLabel.Text = message;
        StatusLabel.IsVisible = true;
    }
}
