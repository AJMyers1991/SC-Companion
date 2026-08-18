using System.Collections.ObjectModel;
using SCCompanion.Data;
using SCCompanion.Data.Caching;
using SCCompanion.Data.Crafting;
using SCCompanion.Data.Entities;
using SCCompanion.Models;

namespace SCCompanion.Views;

public partial class CraftingPage : ContentPage
{
    private const string FavoriteCategory = "crafting-blueprint";
    private static readonly Uri ProviderUri = new("https://sc-craft.tools");

    private readonly AppDatabase _database;
    private readonly CraftingBlueprintService _blueprintService;
    private readonly CraftingImageService _imageService;
    private readonly DiskResourceCache _resourceCache;
    private readonly Dictionary<long, CraftingBlueprint> _knownBlueprints = [];
    private HashSet<long> _favoriteIds = [];
    private CancellationTokenSource? _searchCancellation;
    private bool _isOpening;

    public CraftingPage(
        AppDatabase database,
        CraftingBlueprintService blueprintService,
        CraftingImageService imageService,
        DiskResourceCache resourceCache)
    {
        _database = database;
        _blueprintService = blueprintService;
        _imageService = imageService;
        _resourceCache = resourceCache;
        InitializeComponent();
        BindingContext = this;
    }

    public ObservableCollection<CraftingBlueprintCardItem> Results { get; } = [];
    public ObservableCollection<CraftingBlueprintCardItem> RecentItems { get; } = [];
    public ObservableCollection<CraftingBlueprintCardItem> FavoriteItems { get; } = [];

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadSavedSectionsAsync();
        UpdatePresentation();
    }

    protected override void OnDisappearing()
    {
        _searchCancellation?.Cancel();
        base.OnDisappearing();
    }

    private async Task LoadSavedSectionsAsync()
    {
        try
        {
            Task<IReadOnlyList<FavoriteRecord>> favoritesTask =
                _database.GetFavoritesAsync(FavoriteCategory);
            Task<IReadOnlyList<CraftingBlueprintSummaryRecord>> recentTask =
                _database.GetRecentCraftingBlueprintsAsync(5);
            await Task.WhenAll(favoritesTask, recentTask);

            IReadOnlyList<FavoriteRecord> favorites = await favoritesTask;
            _favoriteIds = favorites
                .Select(item => long.TryParse(item.ExternalId, out long id) ? id : 0)
                .Where(id => id > 0)
                .ToHashSet();

            RecentItems.Clear();
            foreach (CraftingBlueprintSummaryRecord summary in await recentTask)
            {
                CraftingBlueprint blueprint = FromSummary(summary);
                _knownBlueprints[summary.BlueprintId] = blueprint;
                RecentItems.Add(new CraftingBlueprintCardItem(
                    blueprint,
                    _favoriteIds.Contains(summary.BlueprintId)));
            }

            FavoriteItems.Clear();
            foreach (FavoriteRecord favorite in favorites
                         .OrderBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase))
            {
                if (!long.TryParse(favorite.ExternalId, out long id) || id <= 0) continue;
                CraftingBlueprintSummaryRecord? summary =
                    await _database.GetCraftingBlueprintSummaryAsync(id);
                CraftingBlueprint blueprint = summary is not null
                    ? FromSummary(summary)
                    : CreateSummaryBlueprint(id, favorite.DisplayName, string.Empty, 0);
                _knownBlueprints[id] = blueprint;
                FavoriteItems.Add(new CraftingBlueprintCardItem(blueprint, true));
            }

            RefreshVisibleFavoriteState();
        }
        catch (Exception exception)
        {
            System.Diagnostics.Debug.WriteLine($"Unable to load Crafting saved sections: {exception}");
            ShowStatus("Saved Crafting items are temporarily unavailable.");
        }
    }

    private async void OnSearchTextChanged(object? sender, TextChangedEventArgs e)
    {
        _searchCancellation?.Cancel();
        _searchCancellation?.Dispose();
        _searchCancellation = new CancellationTokenSource();
        CancellationToken token = _searchCancellation.Token;
        string query = e.NewTextValue?.Trim() ?? string.Empty;

        if (query.Length < 2)
        {
            Results.Clear();
            SearchActivityIndicator.IsRunning = false;
            SearchActivityIndicator.IsVisible = false;
            StatusLabel.IsVisible = false;
            UpdatePresentation();
            return;
        }

        SearchActivityIndicator.IsRunning = true;
        SearchActivityIndicator.IsVisible = true;
        ShowStatus("Searching blueprints...");
        UpdatePresentation();
        try
        {
            await Task.Delay(400, token);
            IReadOnlyList<CraftingBlueprint> matches =
                await _blueprintService.SearchAsync(query, token);
            token.ThrowIfCancellationRequested();

            Results.Clear();
            foreach (CraftingBlueprint blueprint in matches)
            {
                long id = blueprint.Id ?? 0;
                _knownBlueprints[id] = blueprint;
                Results.Add(new CraftingBlueprintCardItem(
                    blueprint,
                    _favoriteIds.Contains(id)));
            }

            ShowStatus(Results.Count == 0
                ? "No matching blueprints found."
                : $"{Results.Count} {(Results.Count == 1 ? "blueprint" : "blueprints")}");
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            System.Diagnostics.Debug.WriteLine($"Crafting search failed: {exception}");
            Results.Clear();
            ShowStatus("Crafting search is unavailable. Check your connection and try again.");
        }
        finally
        {
            if (!token.IsCancellationRequested)
            {
                SearchActivityIndicator.IsRunning = false;
                SearchActivityIndicator.IsVisible = false;
                UpdatePresentation();
            }
        }
    }

    private async void OnResultSelected(object? sender, SelectionChangedEventArgs e) =>
        await OpenSelectedAsync(ResultsCollectionView, e);

    private async void OnSavedItemTapped(object? sender, TappedEventArgs e)
    {
        if (e.Parameter is CraftingBlueprintCardItem item)
        {
            await OpenItemAsync(item);
        }
    }

    private async Task OpenSelectedAsync(CollectionView collection, SelectionChangedEventArgs e)
    {
        if (_isOpening || e.CurrentSelection.FirstOrDefault() is not CraftingBlueprintCardItem item)
        {
            return;
        }

        collection.SelectedItem = null;
        await OpenItemAsync(item);
    }

    private async Task OpenItemAsync(CraftingBlueprintCardItem item)
    {
        if (_isOpening) return;

        _isOpening = true;
        NavigationLoader.IsRunning = true;
        try
        {
            await Task.Yield();
            CraftingBlueprint blueprint = item.Blueprint;
            if (blueprint.Ingredients is null)
            {
                blueprint = await _blueprintService.FindByIdAsync(
                    item.Id,
                    item.Name) ?? throw new InvalidOperationException("Blueprint detail was not returned by SC Craft.");
                _knownBlueprints[item.Id] = blueprint;
            }

            await SaveSummaryAsync(blueprint, markOpened: true);
            await Navigation.PushAsync(new CraftingDetailPage(
                blueprint,
                _database,
                _imageService,
                _resourceCache));
        }
        catch (Exception exception)
        {
            System.Diagnostics.Debug.WriteLine($"Unable to open Crafting blueprint: {exception}");
            await DisplayAlertAsync(
                "Unable to Open Blueprint",
                "SC Companion could not load that blueprint's details.",
                "OK");
        }
        finally
        {
            NavigationLoader.IsRunning = false;
            _isOpening = false;
        }
    }

    private async void OnFavoriteClicked(object? sender, EventArgs e)
    {
        if (sender is not Button { CommandParameter: CraftingBlueprintCardItem item }) return;
        try
        {
            bool favorite = await _database.ToggleFavoriteAsync(
                FavoriteCategory,
                item.Id.ToString(),
                item.Name);
            if (favorite)
            {
                _favoriteIds.Add(item.Id);
                await SaveSummaryAsync(item.Blueprint, markOpened: false);
            }
            else
            {
                _favoriteIds.Remove(item.Id);
            }

            await LoadSavedSectionsAsync();
            UpdatePresentation();
        }
        catch (Exception exception)
        {
            System.Diagnostics.Debug.WriteLine($"Unable to update Crafting favorite: {exception}");
            await DisplayAlertAsync("Favorite Not Saved", "SC Companion could not update that favorite.", "OK");
        }
    }

    private async Task SaveSummaryAsync(CraftingBlueprint blueprint, bool markOpened)
    {
        if (blueprint.Id is not long id || id <= 0) return;
        await _database.SaveCraftingBlueprintSummaryAsync(
            new CraftingBlueprintSummaryRecord
            {
                BlueprintId = id,
                DisplayName = blueprint.Name?.Trim() ?? "Unknown",
                Category = blueprint.Category?.Trim() ?? string.Empty,
                CraftTimeSeconds = blueprint.CraftTimeSeconds ?? 0
            },
            markOpened);
    }

    private void RefreshVisibleFavoriteState()
    {
        foreach (CraftingBlueprintCardItem item in Results.Concat(RecentItems).Concat(FavoriteItems))
        {
            item.IsFavorite = _favoriteIds.Contains(item.Id);
        }
    }

    private void UpdatePresentation()
    {
        bool showingSearch = (SearchEntry.Text?.Trim().Length ?? 0) >= 2;
        ResultsCollectionView.IsVisible = showingSearch;
        SavedItemsScrollView.IsVisible = !showingSearch &&
                                         (RecentItems.Count > 0 || FavoriteItems.Count > 0);
        RecentSection.IsVisible = !showingSearch && RecentItems.Count > 0;
        FavoritesSection.IsVisible = !showingSearch && FavoriteItems.Count > 0;
        if (!showingSearch && RecentItems.Count == 0 && FavoriteItems.Count == 0)
        {
            ShowStatus("Search for crafting blueprints");
        }
    }

    private async void OnProviderTapped(object? sender, TappedEventArgs e)
    {
        try
        {
            await Browser.Default.OpenAsync(ProviderUri, BrowserLaunchMode.SystemPreferred);
        }
        catch (Exception exception)
        {
            System.Diagnostics.Debug.WriteLine($"Unable to open SC Craft: {exception}");
            await DisplayAlertAsync("Unable to Open Link", "SC Companion could not open sc-craft.tools.", "OK");
        }
    }

    private void OnSearchEntryHandlerChanged(object? sender, EventArgs e)
    {
#if ANDROID
        if (sender is Entry entry && entry.Handler?.PlatformView is AndroidX.AppCompat.Widget.AppCompatEditText nativeEntry)
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

    private static CraftingBlueprint FromSummary(CraftingBlueprintSummaryRecord summary) =>
        CreateSummaryBlueprint(summary.BlueprintId, summary.DisplayName, summary.Category, summary.CraftTimeSeconds);

    private static CraftingBlueprint CreateSummaryBlueprint(long id, string name, string category, int craftTime) =>
        new(id, null, name, null, category, craftTime, null, null, null, null);
}
