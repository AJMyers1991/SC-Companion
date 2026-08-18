using System.Collections.ObjectModel;
using SCCompanion.Data;
using SCCompanion.Data.Caching;
using SCCompanion.Data.Entities;
using SCCompanion.Models;

namespace SCCompanion.Views;

public partial class GuidesPage : ContentPage
{
    private const string FavoriteCategory = "guide-folder";
    private const double MaximumGridWidth = 540;
    private const double HorizontalPagePadding = 24;
    private const double TotalColumnSpacing = 20;

    private readonly AppDatabase _database;
    private readonly DiskResourceCache _resourceCache;
    private readonly List<GuideFolderItem> _allFolders = GuideCatalog.CreateFolders().ToList();

    private GuideFolderItem? _currentFolder;
    private bool _isLoadingFavorites;

    public GuidesPage(AppDatabase database, DiskResourceCache resourceCache)
    {
        _database = database;
        _resourceCache = resourceCache;

        InitializeComponent();
        BindingContext = this;
        RebuildFolders();
        ApplyCurrentView();
    }

    public ObservableCollection<GuideFolderItem> Folders { get; } = [];

    public ObservableCollection<GuideDefinition> VisibleGuides { get; } = [];

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (_isLoadingFavorites)
        {
            return;
        }

        _isLoadingFavorites = true;
        try
        {
            IReadOnlyList<FavoriteRecord> favorites =
                await _database.GetFavoritesAsync(FavoriteCategory);
            HashSet<string> favoriteFolderNames = favorites
                .Select(favorite => favorite.ExternalId)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (GuideFolderItem folder in _allFolders)
            {
                folder.IsFavorite = favoriteFolderNames.Contains(folder.Name);
            }

            RebuildFolders();
        }
        catch (Exception exception)
        {
            System.Diagnostics.Debug.WriteLine($"Unable to load guide-folder favorites: {exception}");
            await DisplayAlertAsync(
                "Favorites Unavailable",
                "Your saved guide-folder favorites could not be loaded.",
                "OK");
        }
        finally
        {
            _isLoadingFavorites = false;
        }
    }

    protected override void OnSizeAllocated(double width, double height)
    {
        base.OnSizeAllocated(width, height);

        if (width <= 0)
        {
            return;
        }

        double gridWidth = Math.Min(width, MaximumGridWidth);
        double cardSize = Math.Clamp(
            (gridWidth - HorizontalPagePadding - TotalColumnSpacing) / 3,
            92,
            160);

        foreach (GuideFolderItem folder in _allFolders)
        {
            folder.CardSize = cardSize;
        }
    }

    protected override bool OnBackButtonPressed()
    {
        if (!string.IsNullOrWhiteSpace(GuideSearchBar.Text))
        {
            GuideSearchBar.Text = string.Empty;
            return true;
        }

        if (_currentFolder is not null)
        {
            _currentFolder = null;
            ApplyCurrentView();
            return true;
        }

        return base.OnBackButtonPressed();
    }

    private void OnSearchTextChanged(object? sender, TextChangedEventArgs e)
    {
        ApplyCurrentView();
    }

    private void OnSearchInputHandlerChanged(object? sender, EventArgs e) =>
        NativeSearchInputChrome.RemoveIosChrome(sender);

    private void OnFolderClicked(object? sender, EventArgs e)
    {
        if (sender is not Button button ||
            button.CommandParameter is not GuideFolderItem folder)
        {
            return;
        }

        _currentFolder = folder;
        GuideSearchBar.Text = string.Empty;
        ApplyCurrentView();
    }

    private async void OnFolderFavoriteClicked(object? sender, EventArgs e)
    {
        if (sender is not ImageButton button ||
            button.CommandParameter is not GuideFolderItem folder)
        {
            return;
        }

        button.IsEnabled = false;
        bool shouldReorder = false;

        try
        {
            folder.IsFavorite = await _database.ToggleFavoriteAsync(
                FavoriteCategory,
                folder.Name,
                folder.Name);
            shouldReorder = true;
        }
        catch (Exception exception)
        {
            System.Diagnostics.Debug.WriteLine($"Unable to toggle guide-folder favorite: {exception}");
            await DisplayAlertAsync(
                "Favorite Not Saved",
                $"SC Companion could not update the favorite status for {folder.Name}.",
                "OK");
        }
        finally
        {
            button.IsEnabled = true;
        }

        if (shouldReorder)
        {
            RebuildFolders();
        }
    }

    private async void OnGuideClicked(object? sender, EventArgs e)
    {
        if (sender is not Button button ||
            button.CommandParameter is not GuideDefinition guide)
        {
            return;
        }

        await Navigation.PushModalAsync(new GuideImageViewerPage(guide, _resourceCache));
    }

    private void RebuildFolders()
    {
        GuideFolderItem[] sortedFolders = _allFolders
            .OrderByDescending(folder => folder.IsFavorite)
            .ThenBy(folder => folder.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Folders.Clear();
        foreach (GuideFolderItem folder in sortedFolders)
        {
            Folders.Add(folder);
        }
    }

    private void ApplyCurrentView()
    {
        string searchQuery = GuideSearchBar.Text?.Trim() ?? string.Empty;

        if (searchQuery.Length > 0)
        {
            GuideDefinition[] searchResults = _allFolders
                .SelectMany(folder => folder.Guides)
                .Where(guide => guide.Name.Contains(searchQuery, StringComparison.OrdinalIgnoreCase))
                .OrderBy(guide => guide.Name, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            ShowGuideList(
                searchResults,
                $"{searchResults.Length} result{(searchResults.Length == 1 ? string.Empty : "s")}");
            return;
        }

        if (_currentFolder is not null)
        {
            GuideDefinition[] folderGuides = _currentFolder.Guides
                .OrderBy(guide => guide.Name, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            ShowGuideList(
                folderGuides,
                $"{folderGuides.Length} guide{(folderGuides.Length == 1 ? string.Empty : "s")}");
            return;
        }

        FolderCollectionView.IsVisible = true;
        GuideCollectionView.IsVisible = false;
        ResultCountLabel.Text = string.Empty;
        ResultCountLabel.IsVisible = false;
        EmptyStateLabel.IsVisible = false;
    }

    private void ShowGuideList(IReadOnlyList<GuideDefinition> guides, string resultCount)
    {
        VisibleGuides.Clear();
        foreach (GuideDefinition guide in guides)
        {
            VisibleGuides.Add(guide);
        }

        FolderCollectionView.IsVisible = false;
        GuideCollectionView.IsVisible = guides.Count > 0;
        ResultCountLabel.Text = resultCount;
        ResultCountLabel.IsVisible = true;
        EmptyStateLabel.IsVisible = guides.Count == 0;
    }
}