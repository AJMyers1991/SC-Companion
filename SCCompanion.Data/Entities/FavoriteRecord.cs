using SQLite;

namespace SCCompanion.Data.Entities;

/// <summary>
/// Identifies an application object that the user has marked as a favorite.
/// </summary>
[Table("Favorites")]
public sealed class FavoriteRecord
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed(Name = "UX_Favorites_Category_ExternalId", Order = 1, Unique = true)]
    public string Category { get; set; } = string.Empty;

    [Indexed(Name = "UX_Favorites_Category_ExternalId", Order = 2, Unique = true)]
    public string ExternalId { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public DateTime CreatedUtc { get; set; }
}
