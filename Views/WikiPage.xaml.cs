using System.Collections.ObjectModel;
using SCCompanion.Data;
using SCCompanion.Data.Search;

namespace SCCompanion.Views;

public partial class WikiPage : ContentPage
{
    private const string RecentSearchFeature = "wiki";
    private static readonly Uri ProviderUri = new("https://starcitizen.tools");

    private readonly AppDatabase _database;
    private readonly WikiSearchService _searchService;

    private CancellationTokenSource? _searchCancellation;
    private bool _isSearchFocused;
    private bool _isOpeningResult;

    public WikiPage(AppDatabase database, WikiSearchService searchService)
    {
        _database = database;
        _searchService = searchService;

        InitializeComponent();
        BindingContext = this;
    }

    public ObservableCollection<string> RecentSearches { get; } = [];

    public ObservableCollection<WikiArticleSearchResult> Results { get; } = [];

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        await LoadRecentSearchesAsync();
        UpdateEmptyPresentation();
    }

    protected override void OnDisappearing()
    {
        CancelSearch();
        base.OnDisappearing();
    }

    private async void OnSearchTextChanged(object? sender, TextChangedEventArgs e)
    {
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
                ShowStatus("Search the Star Citizen Wiki.");
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
            e.CurrentSelection.FirstOrDefault() is not WikiArticleSearchResult article)
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

            Uri articleUri = SearchResultUriBuilder.BuildWikiArticleUri(article.PageId);
            await Navigation.PushModalAsync(
                new SearchResultBrowserPage(article.Title, articleUri));
        }
        catch (Exception exception)
        {
            System.Diagnostics.Debug.WriteLine($"Unable to open Wiki article: {exception}");
            await DisplayAlertAsync(
                "Unable to Open Article",
                "SC Companion could not open that Wiki article.",
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
            System.Diagnostics.Debug.WriteLine($"Unable to open Wiki provider link: {exception}");
            await DisplayAlertAsync(
                "Unable to Open Link",
                "SC Companion could not open starcitizen.tools.",
                "OK");
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
        ShowStatus("Searching the Wiki...");

        try
        {
            await Task.Delay(500, cancellation.Token);
            IReadOnlyList<WikiArticleSearchResult> matches = await _searchService.SearchAsync(
                normalizedQuery,
                cancellation.Token);

            cancellation.Token.ThrowIfCancellationRequested();
            foreach (WikiArticleSearchResult match in matches)
            {
                Results.Add(match);
            }

            ResultsView.IsVisible = Results.Count > 0;
            if (Results.Count == 0)
            {
                ShowStatus("No matching Wiki articles found.");
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
            System.Diagnostics.Debug.WriteLine($"Unable to search the Wiki: {exception}");
            ShowStatus("Wiki search is unavailable. Check your connection and try again.");
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
            System.Diagnostics.Debug.WriteLine($"Unable to load Wiki recent searches: {exception}");
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
            System.Diagnostics.Debug.WriteLine($"Unable to save Wiki recent search: {exception}");
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
            ShowStatus("Search the Star Citizen Wiki.");
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

    private void CancelSearch()
    {
        _searchCancellation?.Cancel();
        _searchCancellation?.Dispose();
        _searchCancellation = null;
        SearchActivityIndicator.IsRunning = false;
        SearchActivityIndicator.IsVisible = false;
    }
}
