using System.Text.Json.Serialization;

namespace SCCompanion.Data.Trade;

/// <summary>
/// Represents the public UEX API response envelope.
/// </summary>
public sealed record UexResponse<T>(
    [property: JsonPropertyName("status")] string? Status,
    [property: JsonPropertyName("http_code")] int? HttpCode,
    [property: JsonPropertyName("data")] T? Data,
    [property: JsonPropertyName("message")] string? Message);

/// <summary>
/// Represents one commodity price and quantity report for one terminal.
/// UEX price fields are named from the player's perspective.
/// </summary>
public sealed record UexPriceEntry(
    [property: JsonPropertyName("id")] long? Id,
    [property: JsonPropertyName("id_commodity")] long? CommodityId,
    [property: JsonPropertyName("id_terminal")] long? TerminalId,
    [property: JsonPropertyName("commodity_name")] string? CommodityName,
    [property: JsonPropertyName("terminal_name")] string? TerminalName,
    [property: JsonPropertyName("price_buy")] double? BuyPrice,
    [property: JsonPropertyName("price_buy_avg")] double? AverageBuyPrice,
    [property: JsonPropertyName("price_sell")] double? SellPrice,
    [property: JsonPropertyName("price_sell_avg")] double? AverageSellPrice,
    [property: JsonPropertyName("scu_buy")] double? BuyQuantity,
    [property: JsonPropertyName("scu_buy_avg")] double? AverageBuyQuantity,
    [property: JsonPropertyName("scu_sell_stock")] double? SellStock,
    [property: JsonPropertyName("scu_sell_stock_avg")] double? AverageSellStock,
    [property: JsonPropertyName("scu_sell")] double? SellQuantity,
    [property: JsonPropertyName("scu_sell_avg")] double? AverageSellQuantity,
    [property: JsonPropertyName("status_buy")] int? BuyStatus,
    [property: JsonPropertyName("status_sell")] int? SellStatus,
    [property: JsonPropertyName("date_modified")] long? DateModified);

public enum TradeAction
{
    Buy,
    Sell
}

public sealed record TradeResult(
    UexPriceEntry Entry,
    TradeAction Action,
    string Commodity,
    string Location,
    double Price,
    double AveragePrice);

public sealed record HotTrade(
    string Commodity,
    UexPriceEntry BuyEntry,
    UexPriceEntry SellEntry,
    double BuyPrice,
    double SellPrice,
    double Profit);
