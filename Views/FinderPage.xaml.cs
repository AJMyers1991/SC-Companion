using System.Collections.ObjectModel;
using SCCompanion.Data;
using SCCompanion.Data.Search;
using SCCompanion.Models;

namespace SCCompanion.Views;

public partial class FinderPage : ContentPage
{
    private const string RecentSearchFeature = "finder";
    private static readonly Uri ProviderUri = new("https://finder.cstone.space");

    private readonly AppDatabase _database;
    private readonly FinderSearchService _searchService;

    private CancellationTokenSource? _pageCancellation;
    private CancellationTokenSource? _searchCancellation;
    private bool _isSearchFocused;
    private bool _isOpeningResult;
    private bool _suppressNextTextSearch;

    public FinderPage(AppDatabase database, FinderSearchService searchService)
    {
        _database = database;
        _searchService = searchService;

        InitializeComponent();
        BindingContext = this;
    }

    public ObservableCollection<string> RecentSearches { get; } = [];

    public ObservableCollection<FinderResultViewItem> Results { get; } = [];

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        CancellationToken pageCancellation = ResetPageCancellation();
        await LoadRecentSearchesAsync();
        UpdateEmptyPresentation();

        string? pendingQuery = FinderNavigationRequest.Consume();
        if (!string.IsNullOrWhiteSpace(pendingQuery))
        {
            _suppressNextTextSearch = true;
            SearchEntry.Text = pendingQuery;
            await SearchAsync(pendingQuery);
            return;
        }

        if (string.IsNullOrWhiteSpace(SearchEntry.Text))
        {
            await PreloadIndexAsync(pageCancellation);
        }
    }

    protected override void OnDisappearing()
    {
        CancelSearch();
        _pageCancellation?.Cancel();
        _pageCancellation?.Dispose();
        _pageCancellation = null;

        base.OnDisappearing();
    }

    private async void OnSearchTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_suppressNextTextSearch)
        {
            _suppressNextTextSearch = false;
            return;
        }

        await SearchAsync(e.NewTextValue ?? string.Empty);
    }

    private async void OnSearchFocused(object? sender, FocusEventArgs e)
    {
        _isSearchFocused = true;
        await LoadRecentSearchesAsync();
        UpdateEmptyPresentation();
    }

    private void OnSearchUnfocused(object? sender, FocusEventArgs e)
    {
        _isSearchFocused = false;
        Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(150), () =>
        {
            if (!SearchEntry.IsFocused && string.IsNullOrWhiteSpace(SearchEntry.Text))
            {
                RecentSection.IsVisible = false;
                ShowStatus("Search for items to find where to buy or rent them.");
            }
        });
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

    private void OnRecentSearchSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is not string query)
        {
            return;
        }

        RecentSearchesView.SelectedItem = null;
        SearchEntry.Text = query;
        SearchEntry.Focus();
    }

    private async void OnResultSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (_isOpeningResult ||
            e.CurrentSelection.FirstOrDefault() is not FinderResultViewItem item)
        {
            return;
        }

        ResultsView.SelectedItem = null;
        _isOpeningResult = true;
        try
        {
            string query = SearchEntry.Text?.Trim() ?? string.Empty;
            if (query.Length > 0)
            {
                await SaveRecentSearchAsync(query);
            }

            Uri resultUri = SearchResultUriBuilder.BuildFinderItemUri(item.Id);
            await Navigation.PushModalAsync(
                new SearchResultBrowserPage(item.Name, resultUri));
        }
        catch (Exception exception)
        {
            System.Diagnostics.Debug.WriteLine($"Unable to open CStone item: {exception}");
            await DisplayAlertAsync(
                "Unable to Open Item",
                "SC Companion could not open that CStone item.",
                "OK");
        }
        finally
        {
            _isOpeningResult = false;
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
            System.Diagnostics.Debug.WriteLine($"Unable to open CStone provider link: {exception}");
            await DisplayAlertAsync(
                "Unable to Open Link",
                "SC Companion could not open finder.cstone.space.",
                "OK");
        }
    }

    private async Task PreloadIndexAsync(CancellationToken cancellationToken)
    {
        SearchActivityIndicator.IsRunning = true;
        SearchActivityIndicator.IsVisible = true;
        ShowStatus("Loading item index...");

        try
        {
            await _searchService.PreloadAsync(cancellationToken);
            if (!cancellationToken.IsCancellationRequested)
            {
                UpdateEmptyPresentation();
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            System.Diagnostics.Debug.WriteLine($"Unable to load CStone item index: {exception}");
            ShowStatus("The item index is unavailable. Check your connection and try again.");
        }
        finally
        {
            SearchActivityIndicator.IsRunning = false;
            SearchActivityIndicator.IsVisible = false;
        }
    }

    private async Task SearchAsync(string query)
    {
        CancelSearch();
        Results.Clear();
        ResultsView.IsVisible = false;
        RecentSection.IsVisible = false;

        string normalizedQuery = query.Trim();
        if (normalizedQuery.Length == 0)
        {
            UpdateEmptyPresentation();
            return;
        }

        if (normalizedQuery.Length < 2)
        {
            ShowStatus("Type at least 2 characters to search.");
            return;
        }

        var cancellation = new CancellationTokenSource();
        _searchCancellation = cancellation;
        SearchActivityIndicator.IsRunning = true;
        SearchActivityIndicator.IsVisible = true;
        ShowStatus("Searching CStone items...");

        try
        {
            await Task.Delay(300, cancellation.Token);
            IReadOnlyList<FinderItem> matches = await _searchService.SearchAsync(
                normalizedQuery,
                cancellation.Token);

            cancellation.Token.ThrowIfCancellationRequested();
            foreach (FinderItem match in matches)
            {
                Results.Add(new FinderResultViewItem(match));
            }

            ResultsView.IsVisible = Results.Count > 0;
            if (Results.Count == 0)
            {
                ShowStatus("No matching items found.");
            }
            else
            {
                HideStatus();
            }
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            System.Diagnostics.Debug.WriteLine($"Unable to search CStone items: {exception}");
            ShowStatus("CStone search is unavailable. Check your connection and try again.");
        }
        finally
        {
            if (ReferenceEquals(_searchCancellation, cancellation))
            {
                SearchActivityIndicator.IsRunning = false;
                SearchActivityIndicator.IsVisible = false;
                _searchCancellation.Dispose();
                _searchCancellation = null;
            }
        }
    }

    private async Task LoadRecentSearchesAsync()
    {
        try
        {
            IReadOnlyList<string> searches = await _database.GetRecentSearchesAsync(
                RecentSearchFeature);
            RecentSearches.Clear();
            foreach (string search in searches)
            {
                RecentSearches.Add(search);
            }
        }
        catch (Exception exception)
        {
            System.Diagnostics.Debug.WriteLine($"Unable to load Finder recent searches: {exception}");
        }
    }

    private async Task SaveRecentSearchAsync(string query)
    {
        try
        {
            await _database.AddRecentSearchAsync(RecentSearchFeature, query);
            await LoadRecentSearchesAsync();
        }
        catch (Exception exception)
        {
            System.Diagnostics.Debug.WriteLine($"Unable to save Finder recent search: {exception}");
        }
    }

    private void UpdateEmptyPresentation()
    {
        if (!string.IsNullOrWhiteSpace(SearchEntry.Text))
        {
            return;
        }

        Results.Clear();
        ResultsView.IsVisible = false;
        bool showRecent = SearchPresentationPolicy.ShouldShowRecentSearches(
            _isSearchFocused,
            SearchEntry.Text,
            RecentSearches.Count);
        RecentSection.IsVisible = showRecent;

        if (showRecent)
        {
            HideStatus();
        }
        else
        {
            ShowStatus("Search for items to find where to buy or rent them.");
        }
    }

    private void ShowStatus(string message)
    {
        StatusLabel.Text = message;
        StatusLabel.IsVisible = true;
    }

    private void HideStatus()
    {
        StatusLabel.IsVisible = false;
    }

    private CancellationToken ResetPageCancellation()
    {
        _pageCancellation?.Cancel();
        _pageCancellation?.Dispose();
        _pageCancellation = new CancellationTokenSource();
        return _pageCancellation.Token;
    }

    private void CancelSearch()
    {
        _searchCancellation?.Cancel();
        _searchCancellation?.Dispose();
        _searchCancellation = null;
        SearchActivityIndicator.IsRunning = false;
        SearchActivityIndicator.IsVisible = false;
    }
}
