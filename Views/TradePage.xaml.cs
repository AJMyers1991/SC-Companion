using System.Collections.ObjectModel;
using System.Globalization;
using SCCompanion.Data.Trade;
using SCCompanion.Models;

namespace SCCompanion.Views;

public partial class TradePage : ContentPage
{
    private static readonly Uri ProviderUri = new("https://uexcorp.space/commodities");

    private readonly UexTradeService _tradeService;
    private readonly List<UexPriceEntry> _entries = [];
    private IReadOnlyList<string> _commodities = [];
    private IReadOnlyList<string> _terminals = [];
    private string? _selectedCommodity;
    private string? _selectedTerminal;
    private AutocompleteKind _autocompleteKind;
    private bool _isLoaded;
    private bool _suppressEntryChanges;

    public TradePage(UexTradeService tradeService)
    {
        _tradeService = tradeService;
        InitializeComponent();
        BindingContext = this;
    }

    public ObservableCollection<TradeCardViewItem> TradeCards { get; } = [];

    public ObservableCollection<string> AutocompleteItems { get; } = [];

    public ObservableCollection<TradeInventorySectionViewItem> InventorySections { get; } = [];

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (!_isLoaded)
        {
            await LoadTradeDataAsync();
        }
    }

    private async Task LoadTradeDataAsync()
    {
        SetLoading(true, "Loading price data from UEX...");
        SetSelectorsEnabled(false);
        try
        {
            IReadOnlyList<UexPriceEntry> entries = await _tradeService.GetEntriesAsync();
            _entries.Clear();
            _entries.AddRange(entries);
            _commodities = UexTradeCalculator.GetCommodityNames(_entries);
            _terminals = UexTradeCalculator.GetTerminalNames(_entries);
            _isLoaded = true;
            ShowHotTrades();
        }
        catch (Exception exception)
        {
            System.Diagnostics.Debug.WriteLine($"Unable to load UEX Trade data: {exception}");
            TradeCards.Clear();
            SetStatus("UEX price data is unavailable. Check your connection and try again.");
        }
        finally
        {
            SetSelectorsEnabled(true);
            LoadingIndicator.IsRunning = false;
            LoadingIndicator.IsVisible = false;
        }
    }

    private void OnCommodityTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_suppressEntryChanges || !_isLoaded)
        {
            return;
        }

        if (!string.Equals(e.NewTextValue, _selectedCommodity, StringComparison.Ordinal))
        {
            _selectedCommodity = null;
        }

        if (CommodityEntry.IsFocused)
        {
            OpenAutocomplete(AutocompleteKind.Commodity, e.NewTextValue);
        }
    }

    private void OnTerminalTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_suppressEntryChanges || !_isLoaded)
        {
            return;
        }

        if (!string.Equals(e.NewTextValue, _selectedTerminal, StringComparison.Ordinal))
        {
            _selectedTerminal = null;
        }

        if (TerminalEntry.IsFocused)
        {
            OpenAutocomplete(AutocompleteKind.Terminal, e.NewTextValue);
        }
    }

    private void OnCommodityFocused(object? sender, FocusEventArgs e)
    {
        if (!string.IsNullOrEmpty(_selectedCommodity))
        {
            SetEntryText(CommodityEntry, string.Empty);
            _selectedCommodity = null;
        }

        OpenAutocomplete(AutocompleteKind.Commodity, CommodityEntry.Text);
    }

    private void OnTerminalFocused(object? sender, FocusEventArgs e)
    {
        if (!string.IsNullOrEmpty(_selectedTerminal))
        {
            SetEntryText(TerminalEntry, string.Empty);
            _selectedTerminal = null;
        }

        OpenAutocomplete(AutocompleteKind.Terminal, TerminalEntry.Text);
    }

    private void OnCommodityDropDownClicked(object? sender, EventArgs e)
    {
        CommodityEntry.Focus();
        OpenAutocomplete(AutocompleteKind.Commodity, CommodityEntry.Text);
    }

    private void OnTerminalDropDownClicked(object? sender, EventArgs e)
    {
        TerminalEntry.Focus();
        OpenAutocomplete(AutocompleteKind.Terminal, TerminalEntry.Text);
    }

    private void OpenAutocomplete(AutocompleteKind kind, string? query)
    {
        if (!_isLoaded)
        {
            return;
        }

        _autocompleteKind = kind;
        IReadOnlyList<string> source = kind == AutocompleteKind.Commodity
            ? _commodities
            : _terminals;
        IReadOnlyList<string> suggestions = UexTradeCalculator.FilterSuggestions(source, query);

        AutocompleteItems.Clear();
        foreach (string suggestion in suggestions)
        {
            AutocompleteItems.Add(suggestion);
        }

        AutocompleteStatusLabel.Text = kind == AutocompleteKind.Commodity
            ? "No matching commodities"
            : "No matching locations";
        AutocompleteStatusLabel.IsVisible = suggestions.Count == 0;
        AutocompleteCollectionView.IsVisible = suggestions.Count > 0;

        double topMargin = kind == AutocompleteKind.Commodity
            ? CommoditySearchHost.Y + CommoditySearchHost.Height + MainContentGrid.Padding.Top
            : TerminalSearchHost.Y + TerminalSearchHost.Height + MainContentGrid.Padding.Top;
        AutocompleteDropDownBorder.Margin = new Thickness(18, Math.Max(0, topMargin), 18, 0);
        AutocompleteLayer.IsVisible = true;
    }

    private void OnAutocompleteItemSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is not string selected)
        {
            return;
        }

        AutocompleteCollectionView.SelectedItem = null;
        if (_autocompleteKind == AutocompleteKind.Commodity)
        {
            _selectedCommodity = selected;
            SetEntryText(CommodityEntry, selected);
            CommodityEntry.Unfocus();
        }
        else
        {
            _selectedTerminal = selected;
            SetEntryText(TerminalEntry, selected);
            TerminalEntry.Unfocus();
        }

        CloseAutocomplete();
    }

    private void OnAutocompleteDismissed(object? sender, EventArgs e) =>
        CloseAutocomplete();

    private void CloseAutocomplete()
    {
        AutocompleteLayer.IsVisible = false;
        AutocompleteItems.Clear();
        AutocompleteCollectionView.SelectedItem = null;
    }

    private void OnBuyClicked(object? sender, EventArgs e)
    {
        CloseAutocomplete();
        ShowResults(UexTradeCalculator.FindBuyResults(
            _entries,
            ActiveCommodity,
            ActiveTerminal));
    }

    private void OnSellClicked(object? sender, EventArgs e)
    {
        CloseAutocomplete();
        ShowResults(UexTradeCalculator.FindSellResults(
            _entries,
            ActiveCommodity,
            ActiveTerminal));
    }

    private void OnClearClicked(object? sender, EventArgs e)
    {
        CloseAutocomplete();
        _selectedCommodity = null;
        _selectedTerminal = null;
        SetEntryText(CommodityEntry, string.Empty);
        SetEntryText(TerminalEntry, string.Empty);
        CommodityEntry.Unfocus();
        TerminalEntry.Unfocus();
        ShowHotTrades();
    }

    private void ShowResults(IReadOnlyList<TradeResult> results)
    {
        TradeCards.Clear();
        foreach (TradeResult result in results)
        {
            TradeCards.Add(TradeCardViewItem.FromResult(result));
        }

        SetStatus(results.Count == 0
            ? "No results"
            : $"{results.Count:N0} results");
    }

    private void ShowHotTrades()
    {
        IReadOnlyList<HotTrade> hotTrades = UexTradeCalculator.ComputeHotTrades(_entries);
        TradeCards.Clear();
        foreach (HotTrade trade in hotTrades)
        {
            TradeCards.Add(TradeCardViewItem.FromHotTrade(trade));
        }

        SetStatus(hotTrades.Count == 0 ? "No Hot Trades available" : "Hot Trades");
    }

    private void OnTradeCardSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is not TradeCardViewItem item)
        {
            return;
        }

        TradeCardsView.SelectedItem = null;
        ShowInventory(item);
    }

    private void ShowInventory(TradeCardViewItem item)
    {
        CloseAutocomplete();
        InventorySections.Clear();
        InventoryTitleLabel.Text = item.Commodity;

        if (item.Result is { } result)
        {
            InventorySections.Add(CreateInventorySection(
                result.Entry,
                result.Action,
                result.Action == TradeAction.Buy ? "Buy Location" : "Sell Location",
                result.Price));
        }
        else if (item.HotTrade is { } hotTrade)
        {
            InventorySections.Add(CreateInventorySection(
                hotTrade.BuyEntry,
                TradeAction.Buy,
                "Buy Location",
                hotTrade.BuyPrice));
            InventorySections.Add(CreateInventorySection(
                hotTrade.SellEntry,
                TradeAction.Sell,
                "Sell Location",
                hotTrade.SellPrice));
        }

        InventoryOverlay.IsVisible = InventorySections.Count > 0;
    }

    private static TradeInventorySectionViewItem CreateInventorySection(
        UexPriceEntry entry,
        TradeAction action,
        string heading,
        double price)
    {
        bool isBuy = action == TradeAction.Buy;
        double? lastQuantity = isBuy ? entry.BuyQuantity : entry.SellQuantity;
        double? averageQuantity = isBuy
            ? entry.AverageBuyQuantity
            : entry.AverageSellQuantity;
        bool hasWarning = UexTradeCalculator.HasUnreliableStatus(entry, action);
        int? status = isBuy ? entry.BuyStatus : entry.SellStatus;

        return new TradeInventorySectionViewItem(
            heading,
            entry.TerminalName ?? "Unknown location",
            $"Price: {price.ToString("N0", CultureInfo.CurrentCulture)} aUEC / SCU",
            isBuy ? "Last reported inventory" : "Last reported sell capacity",
            FormatQuantity(lastQuantity),
            isBuy ? "Average inventory" : "Average sell capacity",
            FormatQuantity(averageQuantity),
            FormatTimestamp(entry.DateModified),
            hasWarning,
            hasWarning
                ? $"UEX status flag: {status?.ToString(CultureInfo.InvariantCulture) ?? "missing"}. This status may be stale or unreliable; the positive price remains included."
                : string.Empty);
    }

    private void OnCloseInventoryClicked(object? sender, EventArgs e) =>
        InventoryOverlay.IsVisible = false;

    private async void OnProviderTapped(object? sender, TappedEventArgs e)
    {
        try
        {
            await Browser.Default.OpenAsync(ProviderUri, BrowserLaunchMode.SystemPreferred);
        }
        catch (Exception exception)
        {
            System.Diagnostics.Debug.WriteLine($"Unable to open UEX provider link: {exception}");
            await DisplayAlertAsync(
                "Unable to Open Link",
                "SC Companion could not open uexcorp.space.",
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
    }

    protected override bool OnBackButtonPressed()
    {
        if (InventoryOverlay.IsVisible)
        {
            InventoryOverlay.IsVisible = false;
            return true;
        }

        if (AutocompleteLayer.IsVisible)
        {
            CloseAutocomplete();
            return true;
        }

        return base.OnBackButtonPressed();
    }

    private string? ActiveCommodity =>
        _selectedCommodity ?? ResolveExactSelection(CommodityEntry.Text, _commodities);

    private string? ActiveTerminal =>
        _selectedTerminal ?? ResolveExactSelection(TerminalEntry.Text, _terminals);

    private static string? ResolveExactSelection(
        string? query,
        IReadOnlyList<string> values)
    {
        string normalizedQuery = query?.Trim() ?? string.Empty;
        return values.FirstOrDefault(value =>
            string.Equals(value, normalizedQuery, StringComparison.OrdinalIgnoreCase));
    }

    private void SetEntryText(Entry entry, string value)
    {
        _suppressEntryChanges = true;
        entry.Text = value;
        _suppressEntryChanges = false;
    }

    private static string FormatQuantity(double? quantity) =>
        quantity is > 0
            ? $"{quantity.Value.ToString("N0", CultureInfo.CurrentCulture)} SCU"
            : "NO DATA";

    private static string FormatTimestamp(long? unixTimestamp)
    {
        if (unixTimestamp is not > 0)
        {
            return "Last update: NO DATA";
        }

        DateTimeOffset timestamp = DateTimeOffset.FromUnixTimeSeconds(unixTimestamp.Value)
            .ToLocalTime();
        return $"Last update: {timestamp:g}";
    }

    private void SetLoading(bool loading, string message)
    {
        LoadingIndicator.IsVisible = loading;
        LoadingIndicator.IsRunning = loading;
        SetStatus(message);
    }

    private void SetStatus(string message)
    {
        StatusLabel.Text = message;
        StatusLabel.IsVisible = true;
    }

    private void SetSelectorsEnabled(bool enabled)
    {
        CommodityEntry.IsEnabled = enabled;
        TerminalEntry.IsEnabled = enabled;
        CommodityDropDownButton.IsEnabled = enabled;
        TerminalDropDownButton.IsEnabled = enabled;
    }

    private enum AutocompleteKind
    {
        Commodity,
        Terminal
    }
}
