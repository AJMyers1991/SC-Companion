using SQLite;

namespace SCCompanion.Data.Entities;

[Table("ShipFleet")]
public sealed class ShipFleetRecord
{
    [PrimaryKey]
    public string ShipId { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public DateTime AddedUtc { get; set; }
}
