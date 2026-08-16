using SQLite;

namespace SCCompanion.Data.Entities;

/// <summary>
/// Stores a named choice made within a feature, such as a selected location.
/// </summary>
[Table("UserSelections")]
public sealed class UserSelectionRecord
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed(Name = "UX_UserSelections_Feature_SelectionKey", Order = 1, Unique = true)]
    public string Feature { get; set; } = string.Empty;

    [Indexed(Name = "UX_UserSelections_Feature_SelectionKey", Order = 2, Unique = true)]
    public string SelectionKey { get; set; } = string.Empty;

    public string SelectedValue { get; set; } = string.Empty;

    public DateTime UpdatedUtc { get; set; }
}
