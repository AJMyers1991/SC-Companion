namespace SCCompanion.Models;

/// <summary>
/// Represents one external resource displayed by the Useful Links page.
/// </summary>
public sealed class UsefulLinkItem : BindableObject
{
    private bool _isFavorite;
    private double _cardSize = 160;
    private double _iconWidthRequest = 72;

    public UsefulLinkItem(
        string name,
        string description,
        string url,
        string iconSource,
        bool usesWideIcon = false)
    {
        Name = name;
        Description = description;
        Url = url;
        IconSource = iconSource;
        UsesWideIcon = usesWideIcon;
    }

    public string Name { get; }

    public string Description { get; }

    public string Url { get; }

    public string IconSource { get; }

    public bool UsesWideIcon { get; }

    public double IconWidthRequest
    {
        get => _iconWidthRequest;
        set
        {
            if (Math.Abs(_iconWidthRequest - value) < 0.5)
            {
                return;
            }

            _iconWidthRequest = value;
            OnPropertyChanged();
        }
    }

    public bool IsFavorite
    {
        get => _isFavorite;
        set
        {
            if (_isFavorite == value)
            {
                return;
            }

            _isFavorite = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(FavoriteIconSource));
            OnPropertyChanged(nameof(FavoriteDescription));
        }
    }

    public string FavoriteIconSource => IsFavorite
        ? "favorite_star_filled.svg"
        : "favorite_star_outline.svg";

    public string FavoriteDescription => IsFavorite
        ? $"Remove {Name} from favorites"
        : $"Add {Name} to favorites";

    public double CardSize
    {
        get => _cardSize;
        set
        {
            if (Math.Abs(_cardSize - value) < 0.5)
            {
                return;
            }

            _cardSize = value;
            OnPropertyChanged();
        }
    }
}
