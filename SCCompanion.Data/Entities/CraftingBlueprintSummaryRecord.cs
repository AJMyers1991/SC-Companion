using SQLite;

namespace SCCompanion.Data.Entities;

/// <summary>
/// Stores display metadata for a Crafting favorite or recently opened blueprint.
/// Favorite identity itself remains in the shared Favorites table.
/// </summary>
[Table("CraftingBlueprintSummaries")]
public sealed class CraftingBlueprintSummaryRecord
{
    [PrimaryKey]
    public long BlueprintId { get; set; }

    public string DisplayName { get; set; } = string.Empty;

    public string Category { get; set; } = string.Empty;

    public int CraftTimeSeconds { get; set; }

    public DateTime? LastOpenedUtc { get; set; }
}
