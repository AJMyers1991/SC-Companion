using SCCompanion.Data;
using SCCompanion.Data.Caching;
using SCCompanion.Data.Ships;
using SCCompanion.Models;

namespace SCCompanion.Views;

public partial class ShipDetailPage : ContentPage
{
    private const string FavoriteCategory = "ship";
    private static readonly Uri ErkulUri = new("https://erkul.games/ships");
    private readonly FleetYardsService _service;
    private readonly DiskResourceCache _cache;
    private readonly AppDatabase _database;
    private FleetYardsShip? _ship;
    private readonly List<ShipImageViewItem> _shipImages = [];
    private readonly List<(FleetYardsPaint Paint, ShipImageViewItem Image)> _paintImages = [];
    private int _shipImageIndex;
    private int _paintIndex;
    private bool _isFavorite;
    private bool _isFavoriteBusy;
    private CancellationTokenSource? _loading;

    public ShipDetailPage(FleetYardsService service, DiskResourceCache cache, AppDatabase database)
    {
        _service = service; _cache = cache; _database = database;
        InitializeComponent();
    }

    public ShipDetailPage Initialize(FleetYardsShip ship)
    {
        _ship = ship;
        ToolbarTitle.Text = ShipNameLabel.Text = ship.Name ?? "Ship";
        ManufacturerLabel.Text = ship.Manufacturer?.Name ?? string.Empty;
        AuecPriceLabel.Text = ship.PriceLabel is null ? string.Empty : ShipDetailCalculator.FormatPrice(ship.PriceLabel.Replace(" aUEC", string.Empty)) + " aUEC";
        PledgePriceLabel.Text = ship.PledgePriceLabel ?? string.Empty;
        PricingCard.IsVisible = ship.PriceLabel is not null || ship.PledgePriceLabel is not null;
        ProductionStatusLabel.Text = ShipPresentationFormatter.StatusLabel(ship.ProductionStatus);
        SaleStatusLabel.Text = ship.OnSale is null ? string.Empty : ship.OnSale.Value ? "Currently For Sale" : "Not For Sale";
        StatusCard.IsVisible = ship.ProductionStatus is not null || ship.OnSale is not null;
        DescriptionLabel.Text = ship.Description ?? string.Empty;
        DescriptionCard.IsVisible = !string.IsNullOrWhiteSpace(ship.Description);
        BuildSpecifications(ship);
        return this;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_ship is null) return;
        string id = ShipKey(_ship);
        _isFavorite = (await _database.GetFavoritesAsync(FavoriteCategory)).Any(x => x.ExternalId == id);
        UpdateFavorite();
        await LoadDetailAsync(_ship);
    }

    protected override void OnDisappearing()
    {
        _loading?.Cancel(); _loading?.Dispose(); _loading = null;
        base.OnDisappearing();
    }

    private async Task LoadDetailAsync(FleetYardsShip ship)
    {
        _loading?.Cancel(); _loading = new CancellationTokenSource();
        CancellationToken token = _loading.Token;
        _shipImages.Clear(); _paintImages.Clear();
        _shipImageIndex = _paintIndex = 0;
        ShipImagesCard.IsVisible = PaintsCard.IsVisible = false;
        DetailLoading.IsRunning = DetailLoading.IsVisible = true;
        _ = CacheShipImagesAsync(ship, token);
        try
        {
            ShipDetailData detail = string.IsNullOrWhiteSpace(ship.Slug) ? new([], [], []) : await _service.GetDetailDataAsync(ship.Slug, token);
            RenderEquipment(detail);
            _ = CachePaintImagesAsync(detail.Paints, token);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception exception) { System.Diagnostics.Debug.WriteLine($"Unable to load ship detail: {exception}"); }
        finally { if (!token.IsCancellationRequested) DetailLoading.IsRunning = DetailLoading.IsVisible = false; }
    }

    private async Task CacheShipImagesAsync(FleetYardsShip ship, CancellationToken token)
    {
        var candidates = new[]
        {
            ("Store", ship.Media?.StoreImage), ("Front", ship.Media?.FrontView), ("Side", ship.Media?.SideView),
            ("Top", ship.Media?.TopView), ("Angled", ship.Media?.AngledView)
        }
        .Where(candidate => candidate.Item2?.BestDetailUrl is not null)
        .DistinctBy(candidate => candidate.Item2!.BestDetailUrl, StringComparer.Ordinal)
        .ToArray();

        IEnumerable<Task> jobs = candidates.Select(async candidate =>
        {
            string url = candidate.Item2!.BestDetailUrl!;
            try
            {
                string path = await _cache.GetOrDownloadAsync(new Uri(url), "ships", $"{ShipKey(ship)}-{candidate.Item1}", token);
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    if (token.IsCancellationRequested) return;
                    _shipImages.Add(new ShipImageViewItem(candidate.Item1, path, url));
                    _shipImages.Sort((left, right) =>
                        Array.FindIndex(candidates, item => item.Item1 == left.Title)
                            .CompareTo(Array.FindIndex(candidates, item => item.Item1 == right.Title)));
                    ShipImagesCard.IsVisible = true;
                    ShowShipImage();
                });
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception exception) { System.Diagnostics.Debug.WriteLine($"Unable to cache ship image: {exception}"); }
        });
        try { await Task.WhenAll(jobs); }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
    }

    private async Task CachePaintImagesAsync(IReadOnlyList<FleetYardsPaint> paints, CancellationToken token)
    {
        var candidates = paints
            .Select((paint, index) => (Paint: paint, Index: index, Url: paint.Media?.StoreImage?.BestDetailUrl))
            .Where(candidate => candidate.Url is not null)
            .DistinctBy(candidate => candidate.Url, StringComparer.Ordinal)
            .ToArray();

        IEnumerable<Task> jobs = candidates.Select(async candidate =>
        {
            string url = candidate.Url!;
            try
            {
                string path = await _cache.GetOrDownloadAsync(
                    new Uri(url),
                    "ship-paints",
                    candidate.Paint.Id ?? candidate.Paint.Slug ?? url,
                    token);
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    if (token.IsCancellationRequested) return;
                    _paintImages.Add((
                        candidate.Paint,
                        new ShipImageViewItem(candidate.Paint.Name ?? "Unknown Paint", path, url)));
                    _paintImages.Sort((left, right) =>
                        Array.FindIndex(candidates, item => ReferenceEquals(item.Paint, left.Paint))
                            .CompareTo(Array.FindIndex(candidates, item => ReferenceEquals(item.Paint, right.Paint))));
                    PaintsCard.IsVisible = true;
                    ShowPaint();
                });
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception exception) { System.Diagnostics.Debug.WriteLine($"Unable to cache paint image: {exception}"); }
        });
        try { await Task.WhenAll(jobs); }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
    }

    private void RenderEquipment(ShipDetailData detail)
    {
        ComponentsStack.Clear();
        foreach (string item in ShipDetailCalculator.BuildComponents(detail.Hardpoints)) ComponentsStack.Add(Text(item));
        ComponentsCard.IsVisible = ComponentsStack.Count > 0;
        WeaponsStack.Clear();
        foreach (ShipEquipmentSection section in ShipDetailCalculator.BuildWeaponSections(detail.Hardpoints))
        {
            WeaponsStack.Add(new Label { Text = section.Heading, FontFamily = "OpenSansSemibold", TextColor = Colors.White, HorizontalTextAlignment = TextAlignment.Center });
            foreach (string item in section.Items) WeaponsStack.Add(Text(item));
        }
        WeaponsCard.IsVisible = WeaponsStack.Count > 0;
        ModulesStack.Clear();
        foreach (FleetYardsModule module in detail.Modules)
        {
            ModulesStack.Add(new Label { Text = module.Name ?? "Unknown Module", FontFamily = "OpenSansSemibold", TextColor = Colors.White, HorizontalTextAlignment = TextAlignment.Center });
            if (!string.IsNullOrWhiteSpace(module.Description)) ModulesStack.Add(Text(module.Description));
            if (module.Metrics?.Cargo is { } cargo) ModulesStack.Add(Text($"Cargo: {cargo} SCU", "#007ACC"));
        }
        ModulesCard.IsVisible = ModulesStack.Count > 0;
    }

    private void BuildSpecifications(FleetYardsShip ship)
    {
        SpecificationsStack.Clear();
        AddSection("Physical Dimensions");
        AddSpec("Length", ship.Metrics?.LengthLabel); AddSpec("Beam", ship.Metrics?.BeamLabel); AddSpec("Height", ship.Metrics?.HeightLabel);
        AddSpec("Mass", ship.Metrics?.MassLabel is { } mass ? $"{mass} kg" : null); AddSpec("Size", ship.Metrics?.SizeLabel);
        AddSection("Crew, Cargo & Fuel");
        if (ship.Crew is { } crew) AddSpec("Crew", crew.Min == crew.Max ? crew.MinLabel ?? crew.Min?.ToString() : $"{crew.MinLabel ?? crew.Min?.ToString()} – {crew.MaxLabel ?? crew.Max?.ToString()}");
        AddSpec("Cargo", ship.Metrics?.CargoLabel);
        if (ship.Metrics?.HydrogenFuelTankSize is > 0 and var hydrogen) AddSpec("Hydrogen Fuel", $"{hydrogen} SCU");
        if (ship.Metrics?.QuantumFuelTankSize is > 0 and var quantum) AddSpec("Quantum Fuel", $"{quantum} SCU");
        AddSection("Performance");
        if (ship.Speeds?.ScmSpeed is { } scm) AddSpec("SCM Speed", $"{scm} m/s");
        if (ship.Speeds?.ScmSpeedBoosted is { } max) AddSpec("Max Speed", $"{max} m/s");
        if (ship.Speeds?.PitchBoosted is { } pitch) AddSpec("Pitch", $"{pitch:N0} m/s");
        if (ship.Speeds?.YawBoosted is { } yaw) AddSpec("Yaw", $"{yaw:N0} m/s");
        if (ship.Speeds?.RollBoosted is { } roll) AddSpec("Roll", $"{roll:N0} m/s");
    }

    private void AddSection(string title) => SpecificationsStack.Add(new Label { Text = title, FontFamily = "OpenSansSemibold", TextColor = Colors.White, HorizontalTextAlignment = TextAlignment.Center, Margin = new Thickness(0, 6, 0, 0) });
    private void AddSpec(string label, string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        var row = new Grid { ColumnDefinitions = [new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)] };
        row.Add(new Label { Text = label, TextColor = Color.FromArgb("#A0A0A0"), FontSize = 13 });
        var valueLabel = new Label { Text = value, TextColor = Colors.White, FontFamily = "OpenSansSemibold", FontSize = 13 };
        row.Add(valueLabel, 1); SpecificationsStack.Add(row);
    }

    private void ShowShipImage()
    {
        if (_shipImages.Count == 0) return; _shipImageIndex = (_shipImageIndex + _shipImages.Count) % _shipImages.Count;
        ShipImage.Source = ImageSource.FromFile(_shipImages[_shipImageIndex].LocalPath); ShipImagePosition.Text = $"{_shipImageIndex + 1} / {_shipImages.Count}"; ShipImageControls.IsVisible = _shipImages.Count > 1;
    }
    private void ShowPaint()
    {
        if (_paintImages.Count == 0) return; _paintIndex = (_paintIndex + _paintImages.Count) % _paintImages.Count;
        var item = _paintImages[_paintIndex]; PaintNameLabel.Text = item.Paint.Name ?? "Unknown Paint"; PaintImage.Source = ImageSource.FromFile(item.Image.LocalPath); PaintPosition.Text = $"{_paintIndex + 1} / {_paintImages.Count}"; PaintControls.IsVisible = _paintImages.Count > 1;
    }

    private void OnPreviousShipImage(object? s, EventArgs e) { _shipImageIndex--; ShowShipImage(); }
    private void OnNextShipImage(object? s, EventArgs e) { _shipImageIndex++; ShowShipImage(); }
    private void OnPreviousPaint(object? s, EventArgs e) { _paintIndex--; ShowPaint(); }
    private void OnNextPaint(object? s, EventArgs e) { _paintIndex++; ShowPaint(); }
    private async void OnShipImageButtonClicked(object? s, EventArgs e) { if (_shipImages.Count > 0) await OpenViewerAsync(_shipImages[_shipImageIndex]); }
    private async void OnPaintImageButtonClicked(object? s, EventArgs e) { if (_paintImages.Count > 0) await OpenViewerAsync(_paintImages[_paintIndex].Image); }
    private async Task OpenViewerAsync(ShipImageViewItem image) => await Navigation.PushModalAsync(new ShipImageViewerPage(_ship?.Name ?? "Ship image", image.LocalPath));
    private void OnPricingTapped(object? s, TappedEventArgs e) => PricingOverlay.IsVisible = true;
    private void OnStatusTapped(object? s, TappedEventArgs e) { BuildStatusDialog(); StatusOverlay.IsVisible = true; }
    private void OnClosePricingClicked(object? s, EventArgs e) => PricingOverlay.IsVisible = false;
    private void OnCloseStatusClicked(object? s, EventArgs e) => StatusOverlay.IsVisible = false;
    private async void OnBackClicked(object? s, EventArgs e) => await Navigation.PopAsync();
    private async void OnErkulTapped(object? s, TappedEventArgs e) => await Browser.Default.OpenAsync(ErkulUri, BrowserLaunchMode.SystemPreferred);

    private async void OnFavoriteClicked(object? s, EventArgs e)
    {
        if (_ship is null || _isFavoriteBusy) return;
        _isFavoriteBusy = true;
        FavoriteButton.IsEnabled = false;
        try
        {
            _isFavorite = await _database.ToggleFavoriteAsync(FavoriteCategory, ShipKey(_ship), _ship.Name ?? "Ship");
            UpdateFavorite();
        }
        catch (Exception exception)
        {
            System.Diagnostics.Debug.WriteLine($"Unable to update ship favorite: {exception}");
            await CommunityToolkit.Maui.Alerts.Toast.Make("Unable to update favorite.").Show();
        }
        finally
        {
            _isFavoriteBusy = false;
            FavoriteButton.IsEnabled = true;
        }
    }
    private void UpdateFavorite() { FavoriteButton.Text = _isFavorite ? "★" : "☆"; FavoriteButton.TextColor = Color.FromArgb(_isFavorite ? "#D8B54A" : "#A0A0A0"); }

    private async void OnPledgeStoreClicked(object? s, EventArgs e)
    {
        if (_ship is null) return; PricingOverlay.IsVisible = false;
        string url = _ship.Links?.StoreUrl ?? $"https://robertsspaceindustries.com/pledge/ships/{_ship.Slug ?? _ship.Name}";
        await Browser.Default.OpenAsync(new Uri(url), BrowserLaunchMode.SystemPreferred);
    }

    private async void OnFinderClicked(object? s, EventArgs e)
    {
        if (_ship is null || Shell.Current is not AppShell shell) return;
        PricingOverlay.IsVisible = false;
        FinderNavigationRequest.Set(_ship.Name ?? _ship.Slug ?? string.Empty);
        await shell.ReturnToMoreRootAsync();
        await Shell.Current.GoToAsync(nameof(FinderPage));
    }

    private void BuildStatusDialog()
    {
        if (_ship is null) return; string status = _ship.ProductionStatus?.ToLowerInvariant() ?? string.Empty;
        StatusDialogText.Text = status is "flight-ready" or "flight ready" ? "Ship is flight ready.\nNo loaner required." : status is "in-concept" or "in concept" ? "Ship is In Concept" : ShipPresentationFormatter.StatusLabel(status);
        LoanersStack.Clear();
        List<string> loaners = _ship.Loaners?.Select(x => x.Name).Where(x => !string.IsNullOrWhiteSpace(x)).Cast<string>().ToList() ?? [];
        if (status is not ("flight-ready" or "flight ready") && loaners.Count > 0)
        {
            LoanersStack.Add(Text($"Current Loaner{(loaners.Count > 1 ? "s" : string.Empty)}:", "#FFFFFF", true));
            foreach (string loaner in loaners) LoanersStack.Add(Text(loaner));
        }
    }

    protected override bool OnBackButtonPressed()
    {
        if (PricingOverlay.IsVisible) { PricingOverlay.IsVisible = false; return true; }
        if (StatusOverlay.IsVisible) { StatusOverlay.IsVisible = false; return true; }
        return base.OnBackButtonPressed();
    }

    private static Label Text(string text, string color = "#D0D0D0", bool bold = false) => new() { Text = text, TextColor = Color.FromArgb(color), FontFamily = bold ? "OpenSansSemibold" : "OpenSansRegular", FontSize = 13, HorizontalTextAlignment = TextAlignment.Center };
    private static string ShipKey(FleetYardsShip ship) => ShipIdentity.GetKey(ship);
}
