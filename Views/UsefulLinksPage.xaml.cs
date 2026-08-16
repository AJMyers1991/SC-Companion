using System.Collections.ObjectModel;
using SCCompanion.Data;
using SCCompanion.Data.Entities;
using SCCompanion.Models;

namespace SCCompanion.Views;

public partial class UsefulLinksPage : ContentPage
{
    private const string FavoriteCategory = "useful-link";
    private const double MaximumGridWidth = 520;
    private const double HorizontalPagePadding = 24;
    private const double CardSpacing = 12;

    private readonly AppDatabase _database;
    private readonly List<UsefulLinkItem> _allLinks =
    [
        new("Erkul Ship Configurator", "Ship configuration", "https://erkul.games/calculator", "link_erkul.png"),
        new("SCDB", "Reference database", "https://scdb.space", "link_scdb.png"),
        new("SCMDB", "Mission database", "https://scmdb.net", "link_scmdb.png"),
        new("UVerse", "Reference database", "https://uverse.space", "link_uverse.png", usesWideIcon: true),
        new("SPViewer", "Ship configuration", "https://www.spviewer.eu", "link_spviewer.png"),
        new("Gallog", "Industrial database", "https://www.gallog.co", "link_gallog.png"),
        new("SCMiner", "Mining database", "https://scminer.rocks", "link_scminer.png", usesWideIcon: true),
        new("Star-Head", "Reference database", "https://star-head.de", "link_starhead.png", usesWideIcon: true),
        new("CCU Game", "CCU chaining tool", "https://ccugame.app/", "link_ccugame.png"),
        new("CStone Item Finder", "Item finder", "https://finder.cstone.space", "link_cstone.png"),
        new("Day One Citizen", "New player information", "https://dayonecitizen.com", "link_dayonecitizen.png"),
        new("FleetYards", "Ship database", "https://fleetyards.net", "link_fleetyards.png"),
        new("SC Craft", "Crafting database", "https://sc-craft.tools", "link_sccraft.png"),
        new("Star Citizen Tools", "Community wiki", "https://starcitizen.tools", "link_starcitizentools.png"),
        new("UEX", "Trade and economy data", "https://uexcorp.space", "link_uex.png")
    ];

    private bool _isLoadingFavorites;

    public UsefulLinksPage(AppDatabase database)
    {
        _database = database;

        InitializeComponent();
        BindingContext = this;
        RebuildVisibleLinks();
    }

    public ObservableCollection<UsefulLinkItem> Links { get; } = [];

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
            IReadOnlyList<FavoriteRecord> storedFavorites =
                await _database.GetFavoritesAsync(FavoriteCategory);
            HashSet<string> favoriteUrls = storedFavorites
                .Select(favorite => favorite.ExternalId)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (UsefulLinkItem link in _allLinks)
            {
                link.IsFavorite = favoriteUrls.Contains(link.Url);
            }

            RebuildVisibleLinks();
        }
        catch (Exception exception)
        {
            System.Diagnostics.Debug.WriteLine($"Unable to load useful-link favorites: {exception}");
            await DisplayAlertAsync(
                "Favorites Unavailable",
                "Your saved link favorites could not be loaded.",
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
            (gridWidth - HorizontalPagePadding - CardSpacing) / 2,
            136,
            242);

        foreach (UsefulLinkItem link in _allLinks)
        {
            link.CardSize = cardSize;
            link.IconWidthRequest = link.UsesWideIcon
                ? Math.Clamp(cardSize - 40, 72, 144)
                : 72;
        }
    }

    private async void OnLinkTapped(object? sender, EventArgs e)
    {
        if (sender is not Button button ||
            button.CommandParameter is not UsefulLinkItem link)
        {
            return;
        }

        try
        {
            await Browser.Default.OpenAsync(new Uri(link.Url), BrowserLaunchMode.SystemPreferred);
        }
        catch (Exception exception)
        {
            System.Diagnostics.Debug.WriteLine($"Unable to open useful link '{link.Url}': {exception}");
            await DisplayAlertAsync(
                "Unable to Open Link",
                $"SC Companion could not open {link.Name}.",
                "OK");
        }
    }

    private async void OnFavoriteClicked(object? sender, EventArgs e)
    {
        if (sender is not ImageButton button ||
            button.CommandParameter is not UsefulLinkItem link)
        {
            return;
        }

        button.IsEnabled = false;
        bool shouldReorder = false;

        try
        {
            link.IsFavorite = await _database.ToggleFavoriteAsync(
                FavoriteCategory,
                link.Url,
                link.Name);
            shouldReorder = true;
        }
        catch (Exception exception)
        {
            System.Diagnostics.Debug.WriteLine($"Unable to toggle useful-link favorite: {exception}");
            await DisplayAlertAsync(
                "Favorite Not Saved",
                $"SC Companion could not update the favorite status for {link.Name}.",
                "OK");
        }
        finally
        {
            button.IsEnabled = true;
        }

        if (shouldReorder)
        {
            RebuildVisibleLinks();
        }
    }

    private void RebuildVisibleLinks()
    {
        UsefulLinkItem[] sortedLinks = _allLinks
            .OrderByDescending(link => link.IsFavorite)
            .ThenBy(link => link.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Links.Clear();
        foreach (UsefulLinkItem link in sortedLinks)
        {
            Links.Add(link);
        }
    }
}