using System.Globalization;
using System.Text.RegularExpressions;

namespace SCCompanion.Data.Ships;

public sealed record ShipEquipmentSection(string Heading, IReadOnlyList<string> Items);

public static partial class ShipDetailCalculator
{
    public static IReadOnlyList<string> BuildComponents(IEnumerable<FleetYardsHardpoint> hardpoints) =>
        hardpoints
            .Where(point => point.Group is "avionic" or "propulsion" or "system")
            .Where(point => point.Name?.StartsWith("hardpoint_", StringComparison.Ordinal) != true)
            .GroupBy(point => $"{point.Name} ({FormatSize(point.MinSize)})")
            .Select(group => group.Count() > 1 ? $"{group.Count()}x {group.Key}" : group.Key)
            .ToArray();

    public static IReadOnlyList<ShipEquipmentSection> BuildWeaponSections(
        IEnumerable<FleetYardsHardpoint> hardpoints)
    {
        FleetYardsHardpoint[] candidates = hardpoints
            .Where(point => point.Group == "weapon")
            .Where(point => point.Name is not null && !GenericSubmountRegex().IsMatch(point.Name))
            .Where(point => point.Name is not ("Torpedo" or "Automated PDS" or "Remote Missile Turret"))
            .ToArray();

        var groups = candidates
            .Where(point => point.Name is not null)
            .GroupBy(point => BuildWeaponLabel(point))
            .Select(group => (Label: group.Key, Items: group.ToArray()))
            .ToArray();
        var categories = new Dictionary<string, List<string>>(StringComparer.Ordinal)
        {
            ["Weapons"] = [], ["Turrets"] = [], ["Missiles"] = [], ["Special"] = []
        };

        foreach ((string label, FleetYardsHardpoint[] items) in groups)
        {
            FleetYardsHardpoint sample = items[0];
            string name = sample.Name?.ToLowerInvariant() ?? string.Empty;
            string groupKey = sample.GroupKey?.ToLowerInvariant() ?? string.Empty;
            string category = name.Contains("turret") || name.Contains("pdc") ||
                              name.Contains("pds") || name.Contains("tractor") ||
                              name.Contains("copilot")
                ? "Turrets"
                : sample.Category == "missile_racks" || groupKey.Contains("missile")
                    ? "Missiles"
                    : name.Contains("emp") || name.Contains("interdiction") ||
                      name.Contains("snare") || name.Contains("quantum damp")
                        ? "Special"
                        : "Weapons";
            categories[category].Add(items.Length > 1 ? $"{items.Length}x {label}" : label);
        }

        return new[] { "Weapons", "Turrets", "Missiles", "Special" }
            .Where(category => categories[category].Count > 0)
            .Select(category => new ShipEquipmentSection(category, categories[category]))
            .ToArray();
    }

    public static string FormatPrice(string price)
    {
        string cleaned = price.Replace(",", string.Empty).Replace(" ", string.Empty);
        return double.TryParse(cleaned, NumberStyles.Float, CultureInfo.InvariantCulture, out double number)
            ? number.ToString("N0", CultureInfo.CurrentCulture)
            : price;
    }

    private static string BuildWeaponLabel(FleetYardsHardpoint point)
    {
        string label = NormalizeWeaponName(point.Name ?? string.Empty);
        if (point.MinSize is { } size) label += $" (S{size})";
        (int count, int? subSize) = DeepSubCount(point.Hardpoints);
        if (count > 0 && subSize is { } childSize) label += $" — {count}x S{childSize}";
        return label;
    }

    private static string NormalizeWeaponName(string name)
    {
        string stripped = name.StartsWith("hardpoint_", StringComparison.Ordinal)
            ? name[10..]
            : name;
        if (stripped.StartsWith("turret_", StringComparison.Ordinal))
        {
            string[] parts = stripped.Split('_');
            if (parts.Contains("remote") &&
                (name.Contains("missile", StringComparison.OrdinalIgnoreCase) || parts.LastOrDefault() == "top"))
                return "Remote Missile Turret";
            if (parts.Contains("remote")) return "Remote Turret";
            string[] descriptors = parts.Skip(1)
                .Where(part => part is not ("left" or "right" or "upper" or "lower"))
                .ToArray();
            return descriptors.Length > 0
                ? $"{TitleWords(string.Join(' ', descriptors))} Turret"
                : "Manned Turret";
        }
        if (stripped.StartsWith("pdc", StringComparison.Ordinal)) return "Point Defense Cannon";
        if (stripped.StartsWith("copilot_turret", StringComparison.Ordinal)) return "Remote Tractor Turret";
        if (stripped == "claw") return "Claw";
        if (stripped.StartsWith("torpedo", StringComparison.Ordinal)) return "Torpedo Rack";
        if (stripped.StartsWith("missile_rack", StringComparison.Ordinal) ||
            stripped.StartsWith("weapon_missile", StringComparison.Ordinal)) return "Missile Rack";
        if (stripped.StartsWith("weapon_gun", StringComparison.Ordinal)) return TitleWords(stripped.Replace('_', ' '));
        return TitleWords(stripped.Replace('_', ' '));
    }

    private static (int Count, int? Size) DeepSubCount(IReadOnlyList<FleetYardsHardpoint>? children)
    {
        if (children is not { Count: > 0 }) return (0, null);
        if (children.All(child => child.Hardpoints is { Count: > 0 }))
        {
            int[] grandchildSizes = children.SelectMany(child => child.Hardpoints!)
                .Select(child => child.MinSize).Where(size => size.HasValue).Select(size => size!.Value).ToArray();
            if (grandchildSizes.Length > 0) return MostCommon(grandchildSizes);
        }
        int[] sizes = children.Select(child => child.MinSize).Where(size => size.HasValue).Select(size => size!.Value).ToArray();
        if (sizes.Length == 0) return (children.Count, null);
        if (sizes.Contains(1) && sizes.Any(size => size > 1)) sizes = sizes.Where(size => size != 1).ToArray();
        return MostCommon(sizes);
    }

    private static (int Count, int? Size) MostCommon(IEnumerable<int> sizes)
    {
        var group = sizes.GroupBy(size => size).OrderByDescending(value => value.Count()).First();
        return (group.Count(), group.Key);
    }

    private static string FormatSize(int? size) => size is { } value ? $"S{value}" : string.Empty;
    private static string TitleWords(string text) => CultureInfo.InvariantCulture.TextInfo.ToTitleCase(text);

    [GeneratedRegex("^S\\d+ (Weapon|Missiles)$")]
    private static partial Regex GenericSubmountRegex();
}
