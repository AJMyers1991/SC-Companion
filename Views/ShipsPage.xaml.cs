using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Maui.Alerts;
using SCCompanion.Data;
using SCCompanion.Data.Ships;
using SCCompanion.Models;

namespace SCCompanion.Views;

public partial class ShipsPage : ContentPage
{
    private const string FavoriteCategory = "ship";
    private static readonly Uri ProviderUri = new("https://fleetyards.net");
    private readonly FleetYardsService _service;
    private readonly AppDatabase _database;
    private IReadOnlyList<FleetYardsShip> _allShips = [];
    private HashSet<string> _favoriteIds = [];
    private HashSet<string> _fleetIds = [];
    private readonly HashSet<string> _selectedManufacturers = [];
    private readonly HashSet<string> _selectedRoles = [];
    private IReadOnlyList<string> _selectionSource = [];
    private SelectionKind _selectionKind;
    private ShipFilters _filters = new();
    private FilterDraft? _filterSnapshot;
    private bool _loaded;

    public ShipsPage(FleetYardsService service, AppDatabase database)
    {
        _service = service;
        _database = database;
        InitializeComponent();
        BindingContext = this;
    }

    public ObservableCollection<ShipCardViewItem> Ships { get; } = [];
    public ObservableCollection<ShipFilterOptionViewItem> SelectionOptions { get; } = [];

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadStateAsync();
    }

    private async Task LoadStateAsync()
    {
        LoadingIndicator.IsVisible = LoadingIndicator.IsRunning = true;
        StatusLabel.Text = "Loading ships...";
        try
        {
            _favoriteIds = (await _database.GetFavoritesAsync(FavoriteCategory)).Select(x => x.ExternalId).ToHashSet(StringComparer.Ordinal);
            _fleetIds = (await _database.GetShipFleetIdsAsync()).ToHashSet(StringComparer.Ordinal);
            if (!_loaded)
            {
                _allShips = await _service.GetAllShipsAsync();
                _loaded = true;
            }
            RefreshResults();
        }
        catch (Exception exception)
        {
            System.Diagnostics.Debug.WriteLine($"Unable to load ships: {exception}");
            StatusLabel.Text = "Ship data is unavailable. Check your connection and try again.";
        }
        finally { LoadingIndicator.IsVisible = LoadingIndicator.IsRunning = false; }
    }

    private void RefreshResults()
    {
        IReadOnlyList<FleetYardsShip> results = ShipCatalogCalculator.Apply(_allShips, SearchEntry.Text, _filters, _favoriteIds, _fleetIds);
        Ships.Clear();
        foreach (FleetYardsShip ship in results)
        {
            string id = ShipIdentity.GetKey(ship);
            Ships.Add(new ShipCardViewItem(ship, _favoriteIds.Contains(id), _fleetIds.Contains(id)));
        }
        StatusLabel.Text = results.Count == 0 ? "No ships found" : _filters.IsActive ? $"Filtered Results · {results.Count} ships" : $"{results.Count} ships";
    }

    private void OnSearchTextChanged(object? sender, TextChangedEventArgs e) { if (_loaded) RefreshResults(); }
    private void OnFilterClicked(object? sender, EventArgs e)
    {
        _filterSnapshot = CaptureFilterDraft();
        FilterOverlay.IsVisible = true;
    }

    private void OnFilterCancelClicked(object? sender, EventArgs e) => CancelFilterEditing();

    private async void OnFilterOkClicked(object? sender, EventArgs e)
    {
        if (!TryReadNumericFilters(out double? minScu, out double? maxScu, out double? minPrice, out double? maxPrice))
        {
            await Toast.Make("Enter finite numbers, with minimum values no greater than maximum values.").Show();
            return;
        }

        _filters = new ShipFilters(
            Manufacturers: _selectedManufacturers.Count > 0 ? new HashSet<string>(_selectedManufacturers) : null,
            Classifications: Values((ShipCheck.IsChecked, "ship"), (GroundCheck.IsChecked, "ground_vehicle")),
            Roles: _selectedRoles.Count > 0 ? new HashSet<string>(_selectedRoles) : null,
            ProductionStatuses: Values((FlightReadyCheck.IsChecked, "flight-ready"), (ConceptCheck.IsChecked, "in-concept")),
            MinScu: minScu, MaxScu: maxScu,
            MinPrice: minPrice, MaxPrice: maxPrice,
            PriceIsAuec: AuecPriceCheck.IsChecked, ShowFavoritesOnly: FavoritesOnlyCheck.IsChecked);
        FilterOverlay.IsVisible = false;
        _filterSnapshot = null;
        RefreshResults();
    }

    private async void OnClearFilterClicked(object? sender, EventArgs e)
    {
        if (!_filters.IsActive) { await Toast.Make("No filters are currently enabled").Show(); return; }
        _filters = new(); ResetFilterControls(); RefreshResults();
    }

    private void OnShowFleetClicked(object? sender, EventArgs e)
    {
        _filters = new ShipFilters(ShowFleetOnly: true);
        ResetFilterControls();
        RefreshResults();
    }

    private void OnManufacturerSelectionClicked(object? sender, EventArgs e) => OpenSelection(
        SelectionKind.Manufacturer,
        "Manufacturer",
        _allShips.Select(ship => ship.Manufacturer?.Name).Where(name => !string.IsNullOrWhiteSpace(name)).Cast<string>()
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray());

    private void OnRoleSelectionClicked(object? sender, EventArgs e) =>
        OpenSelection(SelectionKind.Role, "Role / Focus", ShipCatalogCalculator.RoleBuckets);

    private void OpenSelection(SelectionKind kind, string title, IReadOnlyList<string> source)
    {
        _selectionKind = kind;
        _selectionSource = source;
        SelectionTitleLabel.Text = title;
        SelectionSearchEntry.Text = string.Empty;
        PopulateSelectionOptions(string.Empty);
        SelectionOverlay.IsVisible = true;
    }

    private void OnSelectionSearchChanged(object? sender, TextChangedEventArgs e) => PopulateSelectionOptions(e.NewTextValue ?? string.Empty);

    private void PopulateSelectionOptions(string query)
    {
        IReadOnlySet<string> selected = _selectionKind == SelectionKind.Manufacturer ? _selectedManufacturers : _selectedRoles;
        IEnumerable<string> matches = _selectionSource;
        string normalized = query.Trim();
        if (normalized.Length > 0)
        {
            string[] candidates = matches.Where(value => value.Contains(normalized, StringComparison.OrdinalIgnoreCase)).ToArray();
            matches = candidates.Where(value => value.StartsWith(normalized, StringComparison.OrdinalIgnoreCase))
                .Concat(candidates.Where(value => !value.StartsWith(normalized, StringComparison.OrdinalIgnoreCase)));
        }
        SelectionOptions.Clear();
        foreach (string value in matches) SelectionOptions.Add(new ShipFilterOptionViewItem(value, selected.Contains(value)));
    }

    private void OnSelectionOptionSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is not ShipFilterOptionViewItem item) return;
        SelectionOptionsView.SelectedItem = null;
        item.IsSelected = !item.IsSelected;
        HashSet<string> selected = _selectionKind == SelectionKind.Manufacturer ? _selectedManufacturers : _selectedRoles;
        if (item.IsSelected) selected.Add(item.Value); else selected.Remove(item.Value);
    }

    private void OnSelectionDoneClicked(object? sender, EventArgs e)
    {
        CloseSelectionOverlay();
    }

    private void CloseSelectionOverlay()
    {
        ManufacturerSelectionButton.Text = SelectionSummary(_selectedManufacturers);
        RoleSelectionButton.Text = SelectionSummary(_selectedRoles);
        SelectionOverlay.IsVisible = false;
    }

    private async void OnFavoriteClicked(object? sender, EventArgs e)
    {
        if ((sender as BindableObject)?.BindingContext is not ShipCardViewItem item) return;
        if (item.IsFavoriteBusy) return;
        item.IsFavoriteBusy = true;
        if (sender is Button button) button.IsEnabled = false;
        try
        {
            bool persistedState = await _database.ToggleFavoriteAsync(FavoriteCategory, item.Id, item.Name);
            item.IsFavorite = persistedState;
            if (persistedState) _favoriteIds.Add(item.Id); else _favoriteIds.Remove(item.Id);
            if (_filters.ShowFavoritesOnly) RefreshResults();
        }
        catch (Exception exception)
        {
            System.Diagnostics.Debug.WriteLine($"Unable to update ship favorite: {exception}");
            await Toast.Make("Unable to update favorite.").Show();
        }
        finally
        {
            item.IsFavoriteBusy = false;
            if (sender is Button completedButton) completedButton.IsEnabled = true;
        }
    }

    private async void OnFleetClicked(object? sender, EventArgs e)
    {
        if ((sender as BindableObject)?.BindingContext is not ShipCardViewItem item) return;
        if (item.IsFleetBusy) return;
        item.IsFleetBusy = true;
        if (sender is Button button) button.IsEnabled = false;
        bool requestedState = !item.IsInFleet;
        try
        {
            await _database.SetShipFleetMembershipAsync(item.Id, item.Name, requestedState);
            item.IsInFleet = requestedState;
            if (requestedState) _fleetIds.Add(item.Id); else _fleetIds.Remove(item.Id);
            if (_filters.ShowFleetOnly) RefreshResults();
        }
        catch (Exception exception)
        {
            System.Diagnostics.Debug.WriteLine($"Unable to update My Fleet: {exception}");
            await Toast.Make("Unable to update My Fleet.").Show();
        }
        finally
        {
            item.IsFleetBusy = false;
            if (sender is Button completedButton) completedButton.IsEnabled = true;
        }
    }

    private async void OnShipSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is not ShipCardViewItem item) return;
        ShipsView.SelectedItem = null;
        await Navigation.PushAsync(MauiProgram.Services.GetRequiredService<ShipDetailPage>().Initialize(item.Ship));
    }

    private async void OnProviderTapped(object? sender, TappedEventArgs e) => await Browser.Default.OpenAsync(ProviderUri, BrowserLaunchMode.SystemPreferred);

    private void OnSearchEntryHandlerChanged(object? sender, EventArgs e)
    {
#if ANDROID
        if (sender is Entry entry && entry.Handler?.PlatformView is AndroidX.AppCompat.Widget.AppCompatEditText native)
            native.BackgroundTintList = Android.Content.Res.ColorStateList.ValueOf(Android.Graphics.Color.Transparent);
#endif
    }

    protected override bool OnBackButtonPressed()
    {
        if (SelectionOverlay.IsVisible) { CloseSelectionOverlay(); return true; }
        if (FilterOverlay.IsVisible) { CancelFilterEditing(); return true; }
        return base.OnBackButtonPressed();
    }

    private void ResetFilterControls()
    {
        _selectedManufacturers.Clear(); _selectedRoles.Clear();
        ManufacturerSelectionButton.Text = RoleSelectionButton.Text = "All";
        ShipCheck.IsChecked = GroundCheck.IsChecked = FlightReadyCheck.IsChecked = ConceptCheck.IsChecked = FavoritesOnlyCheck.IsChecked = AuecPriceCheck.IsChecked = false;
        MinScuEntry.Text = MaxScuEntry.Text = MinPriceEntry.Text = MaxPriceEntry.Text = string.Empty;
    }

    private bool TryReadNumericFilters(out double? minScu, out double? maxScu, out double? minPrice, out double? maxPrice)
    {
        bool minScuValid = ShipFilterInput.TryParseOptionalFinite(MinScuEntry.Text, out minScu);
        bool maxScuValid = ShipFilterInput.TryParseOptionalFinite(MaxScuEntry.Text, out maxScu);
        bool minPriceValid = ShipFilterInput.TryParseOptionalFinite(MinPriceEntry.Text, out minPrice);
        bool maxPriceValid = ShipFilterInput.TryParseOptionalFinite(MaxPriceEntry.Text, out maxPrice);
        return minScuValid && maxScuValid && minPriceValid && maxPriceValid &&
               ShipFilterInput.IsOrdered(minScu, maxScu) &&
               ShipFilterInput.IsOrdered(minPrice, maxPrice);
    }

    private FilterDraft CaptureFilterDraft() => new(
        new HashSet<string>(_selectedManufacturers), new HashSet<string>(_selectedRoles),
        ShipCheck.IsChecked, GroundCheck.IsChecked, FlightReadyCheck.IsChecked, ConceptCheck.IsChecked,
        FavoritesOnlyCheck.IsChecked, AuecPriceCheck.IsChecked,
        MinScuEntry.Text, MaxScuEntry.Text, MinPriceEntry.Text, MaxPriceEntry.Text);

    private void CancelFilterEditing()
    {
        if (_filterSnapshot is { } snapshot) RestoreFilterDraft(snapshot);
        _filterSnapshot = null;
        FilterOverlay.IsVisible = false;
    }

    private void RestoreFilterDraft(FilterDraft draft)
    {
        _selectedManufacturers.Clear(); _selectedManufacturers.UnionWith(draft.Manufacturers);
        _selectedRoles.Clear(); _selectedRoles.UnionWith(draft.Roles);
        ManufacturerSelectionButton.Text = SelectionSummary(_selectedManufacturers);
        RoleSelectionButton.Text = SelectionSummary(_selectedRoles);
        ShipCheck.IsChecked = draft.Ship; GroundCheck.IsChecked = draft.Ground;
        FlightReadyCheck.IsChecked = draft.FlightReady; ConceptCheck.IsChecked = draft.Concept;
        FavoritesOnlyCheck.IsChecked = draft.FavoritesOnly; AuecPriceCheck.IsChecked = draft.PriceIsAuec;
        MinScuEntry.Text = draft.MinScu; MaxScuEntry.Text = draft.MaxScu;
        MinPriceEntry.Text = draft.MinPrice; MaxPriceEntry.Text = draft.MaxPrice;
    }
    private static IReadOnlySet<string>? Values(params (bool Selected, string Value)[] values)
    {
        HashSet<string> selected = values.Where(x => x.Selected).Select(x => x.Value).ToHashSet(StringComparer.Ordinal);
        return selected.Count == 0 ? null : selected;
    }

    private static string SelectionSummary(IReadOnlySet<string> values) => values.Count switch
    {
        0 => "All",
        <= 2 => string.Join(", ", values.Order(StringComparer.Ordinal)),
        _ => $"{values.Count} selected"
    };

    private enum SelectionKind { Manufacturer, Role }

    private sealed record FilterDraft(
        HashSet<string> Manufacturers, HashSet<string> Roles,
        bool Ship, bool Ground, bool FlightReady, bool Concept,
        bool FavoritesOnly, bool PriceIsAuec,
        string? MinScu, string? MaxScu, string? MinPrice, string? MaxPrice);
}
