using SQLite;

namespace SCCompanion.Data.Entities;

/// <summary>
/// Stores one durable search term within a page-specific history.
/// </summary>
[Table("RecentSearches")]
public sealed class RecentSearchRecord
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed(Name = "UX_RecentSearches_Feature_NormalizedQuery", Order = 1, Unique = true)]
    public string Feature { get; set; } = string.Empty;

    [Indexed(Name = "UX_RecentSearches_Feature_NormalizedQuery", Order = 2, Unique = true)]
    public string NormalizedQuery { get; set; } = string.Empty;

    public string Query { get; set; } = string.Empty;

    public DateTime LastUsedUtc { get; set; }
}
