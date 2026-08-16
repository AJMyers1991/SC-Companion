using SQLite;

namespace SCCompanion.Data.Entities;

/// <summary>
/// Stores the quantity owned for one required item in a Wikelo trade.
/// </summary>
[Table("WikeloTradeProgress")]
public sealed class WikeloTradeProgressRecord
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed(Name = "UX_WikeloTradeProgress_TradeIngredient", Order = 1, Unique = true)]
    public string TradeId { get; set; } = string.Empty;

    [Indexed(Name = "UX_WikeloTradeProgress_TradeIngredient", Order = 2, Unique = true)]
    public string IngredientId { get; set; } = string.Empty;

    public int OwnedQuantity { get; set; }

    public DateTime UpdatedUtc { get; set; }
}
