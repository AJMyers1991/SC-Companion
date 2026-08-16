using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using SCCompanion.Data.Ships;

namespace SCCompanion.Models;

public sealed class ShipCardViewItem : INotifyPropertyChanged
{
    private bool _isFavorite;
    private bool _isInFleet;
    private bool _isFavoriteBusy;
    private bool _isFleetBusy;

    public ShipCardViewItem(FleetYardsShip ship, bool isFavorite, bool isInFleet)
    {
        Ship = ship;
        _isFavorite = isFavorite;
        _isInFleet = isInFleet;
    }

    public FleetYardsShip Ship { get; }
    public string Id => ShipIdentity.GetKey(Ship);
    public string Name => Ship.Name ?? "Unknown";
    public string Manufacturer => Ship.Manufacturer?.Name ?? string.Empty;
    public string ClassificationAndRole => Join(Ship.ClassificationLabel, Ship.Focus);
    public string CargoAndCrew
    {
        get
        {
            string? crew = Ship.Crew is null ? null : $"{Ship.Crew.MinLabel ?? Ship.Crew.Min?.ToString() ?? "?"}–{Ship.Crew.MaxLabel ?? Ship.Crew.Max?.ToString() ?? "?"} Crew";
            return Join(Ship.Metrics?.CargoLabel, crew);
        }
    }
    public string PriceLine => Join(Ship.PledgePriceLabel, Ship.PriceLabel);
    public string? ImageUrl => Ship.Media?.StoreImage?.BestListUrl ?? Ship.Media?.FrontView?.BestListUrl;
    public string FavoriteGlyph => IsFavorite ? "★" : "☆";
    public string FleetGlyph => IsInFleet ? "☑" : "☐";
    public Color FavoriteColor => IsFavorite ? Color.FromArgb("#D8B54A") : Color.FromArgb("#A0A0A0");
    public Color FleetColor => IsInFleet ? Color.FromArgb("#65A86D") : Color.FromArgb("#A0A0A0");

    public bool IsFavorite
    {
        get => _isFavorite;
        set { if (_isFavorite == value) return; _isFavorite = value; Notify(); Notify(nameof(FavoriteGlyph)); Notify(nameof(FavoriteColor)); }
    }

    public bool IsInFleet
    {
        get => _isInFleet;
        set { if (_isInFleet == value) return; _isInFleet = value; Notify(); Notify(nameof(FleetGlyph)); Notify(nameof(FleetColor)); }
    }

    public bool IsFavoriteBusy
    {
        get => _isFavoriteBusy;
        set { if (_isFavoriteBusy == value) return; _isFavoriteBusy = value; Notify(); }
    }

    public bool IsFleetBusy
    {
        get => _isFleetBusy;
        set { if (_isFleetBusy == value) return; _isFleetBusy = value; Notify(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void Notify([CallerMemberName] string? propertyName = null) => PropertyChanged?.Invoke(this, new(propertyName));
    private static string Join(string? first, string? second) => string.Join(" / ", new[] { first, second }.Where(value => !string.IsNullOrWhiteSpace(value)));
}

public sealed record ShipImageViewItem(string Title, string LocalPath, string SourceUrl);

public sealed class ShipFilterOptionViewItem : INotifyPropertyChanged
{
    private bool _isSelected;

    public ShipFilterOptionViewItem(string value, bool isSelected)
    {
        Value = value;
        _isSelected = isSelected;
    }

    public string Value { get; }
    public string CheckGlyph => IsSelected ? "☑" : "☐";
    public Color CheckColor => IsSelected ? Color.FromArgb("#65A86D") : Color.FromArgb("#A0A0A0");

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value) return;
            _isSelected = value;
            Notify();
            Notify(nameof(CheckGlyph));
            Notify(nameof(CheckColor));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void Notify([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new(propertyName));
}
public sealed record ShipSpecViewItem(string Label, string Value);
public sealed record ShipTextLineViewItem(string Text);
public sealed record ShipEquipmentSectionViewItem(string Heading, IReadOnlyList<ShipTextLineViewItem> Items);
public sealed record ShipModuleViewItem(string Name, string Description, string CargoText)
{
    public bool HasDescription => !string.IsNullOrWhiteSpace(Description);
    public bool HasCargo => !string.IsNullOrWhiteSpace(CargoText);
}

public static class ShipPresentationFormatter
{
    public static string StatusLabel(string? status) => string.IsNullOrWhiteSpace(status)
        ? string.Empty
        : CultureInfo.InvariantCulture.TextInfo.ToTitleCase(status.Replace('-', ' '));

    public static string Number(double value, string suffix) => $"{value.ToString("N0", CultureInfo.CurrentCulture)} {suffix}";
}
