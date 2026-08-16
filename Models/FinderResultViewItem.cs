using SCCompanion.Data.Search;

namespace SCCompanion.Models;

/// <summary>
/// Adds MAUI-specific presentation values to a CStone search result.
/// </summary>
public sealed class FinderResultViewItem
{
    public FinderResultViewItem(FinderItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        Id = item.Id;
        Name = item.Name;
        IsAvailable = item.IsAvailable;
    }

    public string Id { get; }

    public string Name { get; }

    public bool IsAvailable { get; }

    public string AvailabilityText => IsAvailable ? "Available" : string.Empty;

    public Color AvailabilityColor => IsAvailable
        ? Color.FromArgb("#129600")
        : Colors.Transparent;
}
