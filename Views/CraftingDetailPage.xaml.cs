using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using SCCompanion.Data;
using SCCompanion.Data.Caching;
using SCCompanion.Data.Crafting;
using SCCompanion.Models;

namespace SCCompanion.Views;

public partial class CraftingDetailPage : ContentPage, INotifyPropertyChanged
{
    private const string FavoriteCategory = "crafting-blueprint";
    private readonly CraftingBlueprint _blueprint;
    private readonly AppDatabase _database;
    private readonly CraftingImageService _imageService;
    private readonly DiskResourceCache _resourceCache;
    private readonly Dictionary<string, int> _qualities = new(StringComparer.OrdinalIgnoreCase);
    private CancellationTokenSource? _pageCancellation;
    private bool _isFavorite;
    private bool _isImageLoading;
    private string _imageSource = "link_sccraft.png";
    private double _globalQuality = CraftingQualityCalculator.NeutralQuality;
    private string _globalQualityText = CraftingQualityCalculator.NeutralQuality.ToString();
    private bool _isApplyingCalculation;

    public CraftingDetailPage(
        CraftingBlueprint blueprint,
        AppDatabase database,
        CraftingImageService imageService,
        DiskResourceCache resourceCache)
    {
        _blueprint = blueprint;
        _database = database;
        _imageService = imageService;
        _resourceCache = resourceCache;
        InitializeComponent();
        BindingContext = this;
        BuildMissions();
        ApplyCalculation();
    }

    public new string Title => _blueprint.Name?.Trim() ?? "Blueprint";
    public string CategoryText => string.IsNullOrWhiteSpace(_blueprint.Category)
        ? string.Empty
        : $"Category: {_blueprint.Category.Trim()}";
    public string CraftTimeText => _blueprint.CraftTimeSeconds is > 0
        ? $"Craft Time: {FormatTime(_blueprint.CraftTimeSeconds.Value)}"
        : string.Empty;
    public string TiersText => _blueprint.Tiers is > 0 ? $"Tiers: {_blueprint.Tiers}" : string.Empty;
    public string FavoriteGlyph => IsFavorite ? "★" : "☆";
    public Color FavoriteColor => IsFavorite ? Color.FromArgb("#FFD700") : Color.FromArgb("#A0A0A0");
    public ObservableCollection<CraftingIngredientViewItem> Ingredients { get; } = [];
    public ObservableCollection<CraftingStatViewItem> StatRows { get; } = [];
    public ObservableCollection<CraftingMissionViewItem> Missions { get; } = [];
    public bool HasStatRows => StatRows.Count > 0;
    public bool HasMissions => Missions.Count > 0;

    public bool IsFavorite
    {
        get => _isFavorite;
        private set
        {
            if (_isFavorite == value) return;
            _isFavorite = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(FavoriteGlyph));
            OnPropertyChanged(nameof(FavoriteColor));
        }
    }

    public bool IsImageLoading
    {
        get => _isImageLoading;
        private set { if (_isImageLoading != value) { _isImageLoading = value; OnPropertyChanged(); } }
    }

    public string ImageSource
    {
        get => _imageSource;
        private set { if (_imageSource != value) { _imageSource = value; OnPropertyChanged(); } }
    }

    public double GlobalQuality
    {
        get => _globalQuality;
        private set { if (Math.Abs(_globalQuality - value) > 0.01d) { _globalQuality = value; OnPropertyChanged(); } }
    }

    public string GlobalQualityText
    {
        get => _globalQualityText;
        private set { if (_globalQualityText != value) { _globalQualityText = value; OnPropertyChanged(); } }
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        _pageCancellation?.Cancel();
        _pageCancellation?.Dispose();
        _pageCancellation = new CancellationTokenSource();
        try
        {
            string id = (_blueprint.Id ?? 0).ToString();
            IReadOnlyList<SCCompanion.Data.Entities.FavoriteRecord> favorites =
                await _database.GetFavoritesAsync(FavoriteCategory);
            IsFavorite = favorites.Any(item => item.ExternalId == id);
            await LoadImageAsync(_pageCancellation.Token);
        }
        catch (OperationCanceledException) when (_pageCancellation.IsCancellationRequested)
        {
        }
    }

    protected override void OnDisappearing()
    {
        _pageCancellation?.Cancel();
        _pageCancellation?.Dispose();
        _pageCancellation = null;
        base.OnDisappearing();
    }

    private async Task LoadImageAsync(CancellationToken cancellationToken)
    {
        long id = _blueprint.Id ?? 0;
        string cacheKey = $"blueprint-{id}";
        string? cached = _resourceCache.TryGetCachedPath("crafting-images", cacheKey);
        if (!string.IsNullOrWhiteSpace(cached))
        {
            ImageSource = cached;
            return;
        }

        IsImageLoading = true;
        try
        {
            string? remote = await _imageService.FindImageAsync(Title, cancellationToken);
            if (!string.IsNullOrWhiteSpace(remote) && Uri.TryCreate(remote, UriKind.Absolute, out Uri? uri))
            {
                ImageSource = await _resourceCache.GetOrDownloadAsync(
                    uri,
                    "crafting-images",
                    cacheKey,
                    cancellationToken);
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            System.Diagnostics.Debug.WriteLine($"Unable to load Crafting image: {exception}");
            ImageSource = "link_sccraft.png";
        }
        finally
        {
            IsImageLoading = false;
        }
    }

    private async void OnFavoriteClicked(object? sender, EventArgs e)
    {
        if (_blueprint.Id is not long id || id <= 0) return;
        try
        {
            IsFavorite = await _database.ToggleFavoriteAsync(
                FavoriteCategory,
                id.ToString(),
                Title);
            if (IsFavorite)
            {
                await _database.SaveCraftingBlueprintSummaryAsync(
                    new SCCompanion.Data.Entities.CraftingBlueprintSummaryRecord
                    {
                        BlueprintId = id,
                        DisplayName = Title,
                        Category = _blueprint.Category?.Trim() ?? string.Empty,
                        CraftTimeSeconds = _blueprint.CraftTimeSeconds ?? 0
                    },
                    markOpened: false);
            }
        }
        catch (Exception exception)
        {
            System.Diagnostics.Debug.WriteLine($"Unable to update Crafting favorite: {exception}");
            await DisplayAlertAsync("Favorite Not Saved", "SC Companion could not update that favorite.", "OK");
        }
    }

    private void OnGlobalQualityChanged(object? sender, ValueChangedEventArgs e)
    {
        if (_isApplyingCalculation) return;
        int quality = Math.Clamp((int)Math.Round(e.NewValue), 0, 1000);
        GlobalQuality = quality;
        GlobalQualityText = quality.ToString();
        IReadOnlyList<CraftingIngredient> ingredients = _blueprint.Ingredients ?? [];
        for (int index = 0; index < ingredients.Count; index++)
        {
            _qualities[CraftingQualityCalculator.BuildIngredientKey(ingredients[index], index)] = quality;
        }
        ApplyCalculation();
    }

    private void OnGlobalQualityCompleted(object? sender, EventArgs e) => ApplyGlobalEntry();
    private void OnGlobalQualityUnfocused(object? sender, FocusEventArgs e) => ApplyGlobalEntry();

    private void ApplyGlobalEntry()
    {
        if (int.TryParse(GlobalQualityEntry.Text, out int value))
        {
            GlobalQualitySlider.Value = Math.Clamp(value, 0, 1000);
        }
        else
        {
            GlobalQualityText = ((int)GlobalQuality).ToString();
        }
    }

    private void OnIngredientQualityChanged(object? sender, ValueChangedEventArgs e)
    {
        if (_isApplyingCalculation || sender is not Slider { BindingContext: CraftingIngredientViewItem item }) return;
        int quality = Math.Clamp((int)Math.Round(e.NewValue), 0, 1000);
        _qualities[item.Key] = quality;
        item.Quality = quality;
        ApplyCalculation();
    }

    private void OnIngredientQualityCompleted(object? sender, EventArgs e) => ApplyIngredientEntry(sender);
    private void OnIngredientQualityUnfocused(object? sender, FocusEventArgs e) => ApplyIngredientEntry(sender);

    private void ApplyIngredientEntry(object? sender)
    {
        if (sender is not Entry { BindingContext: CraftingIngredientViewItem item } entry) return;
        if (int.TryParse(entry.Text, out int value))
        {
            int quality = Math.Clamp(value, 0, 1000);
            _qualities[item.Key] = quality;
            item.Quality = quality;
            ApplyCalculation();
        }
        else
        {
            item.QualityText = ((int)item.Quality).ToString();
        }
    }

    private void ApplyCalculation()
    {
        _isApplyingCalculation = true;
        try
        {
            CraftingQualityCalculation result = CraftingQualityCalculator.Calculate(_blueprint, _qualities);
            if (Ingredients.Count != result.Ingredients.Count)
            {
                Ingredients.Clear();
                foreach (CraftingIngredientQuality ingredient in result.Ingredients)
                {
                    Ingredients.Add(new CraftingIngredientViewItem(ingredient));
                }
            }
            else
            {
                for (int index = 0; index < Ingredients.Count; index++)
                {
                    Ingredients[index].Apply(result.Ingredients[index]);
                }
            }

            StatRows.Clear();
            foreach (CraftingStatSummary summary in result.StatSummary)
            {
                StatRows.Add(new CraftingStatViewItem(summary));
            }
            OnPropertyChanged(nameof(HasStatRows));
        }
        finally
        {
            _isApplyingCalculation = false;
        }
    }

    private void BuildMissions()
    {
        foreach (CraftingMission mission in _blueprint.Missions ?? [])
        {
            Missions.Add(CraftingMissionViewItem.From(mission));
        }
        OnPropertyChanged(nameof(HasMissions));
    }

    private static string FormatTime(int seconds)
    {
        int minutes = seconds / 60;
        int remainder = seconds % 60;
        return minutes > 0 ? $"{minutes}m {remainder}s" : $"{remainder}s";
    }
}
