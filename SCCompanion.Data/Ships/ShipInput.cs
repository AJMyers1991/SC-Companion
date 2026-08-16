using System.Globalization;

namespace SCCompanion.Data.Ships;

public static class ShipIdentity
{
    public static string GetKey(FleetYardsShip ship)
    {
        ArgumentNullException.ThrowIfNull(ship);
        return ship.Id ?? ship.Slug ?? ship.Name ?? string.Empty;
    }
}

public static class ShipFilterInput
{
    public static bool TryParseOptionalFinite(string? value, out double? number)
    {
        number = null;
        if (string.IsNullOrWhiteSpace(value)) return true;
        if (!double.TryParse(value.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed) ||
            !double.IsFinite(parsed))
            return false;
        number = parsed;
        return true;
    }

    public static bool IsOrdered(double? minimum, double? maximum) =>
        minimum is null || maximum is null || minimum <= maximum;
}
