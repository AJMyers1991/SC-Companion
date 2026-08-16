namespace SCCompanion.Models;

public sealed class GuideFolderItem : BindableObject
{
    private bool _isFavorite;
    private double _cardSize = 104;

    public GuideFolderItem(string name, IReadOnlyList<GuideDefinition> guides)
    {
        Name = name;
        Guides = guides;
    }

    public string Name { get; }

    public IReadOnlyList<GuideDefinition> Guides { get; }

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
