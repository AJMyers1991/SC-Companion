using System.ComponentModel;
using System.Runtime.CompilerServices;
using SCCompanion.Data.Crafting;

namespace SCCompanion.Models;

public sealed class CraftingBlueprintCardItem : INotifyPropertyChanged
{
    private bool _isFavorite;

    public CraftingBlueprintCardItem(CraftingBlueprint blueprint, bool isFavorite)
    {
        Blueprint = blueprint;
        _isFavorite = isFavorite;
    }

    public CraftingBlueprint Blueprint { get; }
    public long Id => Blueprint.Id ?? 0;
    public string Name => Blueprint.Name?.Trim() ?? "Unknown";
    public string Category => Blueprint.Category?.Trim() ?? string.Empty;
    public string Classification => Category;
    public int CraftTimeSeconds => Blueprint.CraftTimeSeconds ?? 0;
    public string FavoriteGlyph => IsFavorite ? "★" : "☆";
    public Color FavoriteColor => IsFavorite ? Color.FromArgb("#FFD700") : Color.FromArgb("#A0A0A0");

    public bool IsFavorite
    {
        get => _isFavorite;
        set
        {
            if (_isFavorite == value) return;
            _isFavorite = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(FavoriteGlyph));
            OnPropertyChanged(nameof(FavoriteColor));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public sealed class CraftingIngredientViewItem : INotifyPropertyChanged
{
    private double _quality;
    private string _qualityText;

    public CraftingIngredientViewItem(CraftingIngredientQuality ingredient)
    {
        Key = ingredient.Slot;
        Name = ingredient.Name;
        QuantityText = FormatQuantity(ingredient.Quantity, ingredient.Unit);
        _quality = ingredient.Quality;
        _qualityText = ingredient.Quality.ToString();
        Effects = ingredient.Effects.Select(effect => new CraftingEffectViewItem(effect)).ToArray();
    }

    public string Key { get; }
    public string SlotText => Key.Split('#')[0].ToUpperInvariant();
    public string SlotName => SlotText;
    public string Name { get; }
    public string QuantityText { get; }
    public IReadOnlyList<CraftingEffectViewItem> Effects { get; private set; }
    public IReadOnlyList<CraftingEffectViewItem> EffectRows => Effects;
    public bool HasEffects => Effects.Count > 0;

    public double Quality
    {
        get => _quality;
        set
        {
            if (Math.Abs(_quality - value) < 0.01d) return;
            _quality = value;
            _qualityText = ((int)Math.Round(value)).ToString();
            OnPropertyChanged();
            OnPropertyChanged(nameof(QualityText));
        }
    }

    public string QualityText
    {
        get => _qualityText;
        set
        {
            if (_qualityText == value) return;
            _qualityText = value;
            OnPropertyChanged();
        }
    }

    public void Apply(CraftingIngredientQuality ingredient)
    {
        _quality = ingredient.Quality;
        _qualityText = ingredient.Quality.ToString();
        Effects = ingredient.Effects.Select(effect => new CraftingEffectViewItem(effect)).ToArray();
        OnPropertyChanged(nameof(Quality));
        OnPropertyChanged(nameof(QualityText));
        OnPropertyChanged(nameof(Effects));
        OnPropertyChanged(nameof(EffectRows));
        OnPropertyChanged(nameof(HasEffects));
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    private static string FormatQuantity(double? quantity, string unit)
    {
        if (quantity is null) return string.Empty;
        string value = Math.Abs(quantity.Value - Math.Round(quantity.Value)) < 0.0000001d
            ? Math.Round(quantity.Value).ToString("0")
            : quantity.Value.ToString("0.####");
        return $"{value} {(string.Equals(unit, "unit", StringComparison.OrdinalIgnoreCase) ? "unit" : unit.ToUpperInvariant())}";
    }
}

public sealed record CraftingEffectViewItem(string Text, Color TextColor)
{
    public CraftingEffectViewItem(CraftingQualityEffectResult effect)
        : this(
            $"{effect.Stat}   {effect.Percentage:+0;-0;+0}%",
            effect.Percentage > 0d
                ? Color.FromArgb("#129600")
                : effect.Percentage < 0d
                    ? Color.FromArgb("#FF0000")
                    : Colors.White)
    {
    }
}

public sealed record CraftingStatViewItem(
    string Stat,
    string BaseValue,
    string CraftedValue,
    Color CraftedColor)
{
    public CraftingStatViewItem(CraftingStatSummary summary)
        : this(
            summary.Stat,
            summary.BaseValue,
            summary.CraftedValue,
            summary.Percentage > 0d
                ? Color.FromArgb("#129600")
                : summary.Percentage < 0d
                    ? Color.FromArgb("#FF0000")
                    : Colors.White)
    {
    }
}

public sealed record CraftingMissionViewItem(
    string Name,
    string ContractorText,
    string LocationText,
    string DropChanceText)
{
    public static CraftingMissionViewItem From(CraftingMission mission)
    {
        string dropChance = string.Empty;
        if (double.TryParse(mission.DropChance, out double chance))
        {
            dropChance = chance < 1d ? $"{chance * 100d:0}%" : "100%";
        }

        return new CraftingMissionViewItem(
            mission.Name?.Trim() ?? "Unknown mission",
            mission.Contractor?.Trim() ?? string.Empty,
            mission.Locations?.Trim() ?? string.Empty,
            dropChance);
    }
}
