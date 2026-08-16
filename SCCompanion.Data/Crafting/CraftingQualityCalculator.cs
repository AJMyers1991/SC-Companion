namespace SCCompanion.Data.Crafting;

public sealed record CraftingIngredientQuality(
    string Slot,
    string Name,
    double? Quantity,
    string Unit,
    int Quality,
    IReadOnlyList<CraftingQualityEffectResult> Effects);

public sealed record CraftingQualityEffectResult(
    string Stat,
    double Percentage,
    bool LowerIsBetter);

public sealed record CraftingStatSummary(
    string Stat,
    string BaseValue,
    string CraftedValue,
    double Percentage,
    bool LowerIsBetter);

public sealed record CraftingQualityCalculation(
    IReadOnlyList<CraftingIngredientQuality> Ingredients,
    IReadOnlyList<CraftingStatSummary> StatSummary);

/// <summary>
/// Reproduces SC Craft's direction-aware material-quality calculations.
/// Positive display percentages always represent an improvement.
/// </summary>
public static class CraftingQualityCalculator
{
    public const int MinimumQuality = 0;
    public const int MaximumQuality = 1000;
    public const int NeutralQuality = 500;

    public static CraftingQualityCalculation Calculate(
        CraftingBlueprint blueprint,
        IReadOnlyDictionary<string, int>? qualities = null)
    {
        ArgumentNullException.ThrowIfNull(blueprint);
        IReadOnlyList<CraftingIngredient> sourceIngredients = blueprint.Ingredients ?? [];
        var ingredients = new List<CraftingIngredientQuality>(sourceIngredients.Count);
        var combined = new Dictionary<string, CombinedEffect>(StringComparer.OrdinalIgnoreCase);

        for (int index = 0; index < sourceIngredients.Count; index++)
        {
            CraftingIngredient source = sourceIngredients[index];
            CraftingIngredientOption? option = source.Options?.FirstOrDefault();
            string slot = source.Slot?.Trim() ?? string.Empty;
            string key = BuildIngredientKey(source, index);
            int quality = Math.Clamp(
                qualities is not null && qualities.TryGetValue(key, out int savedQuality)
                    ? savedQuality
                    : NeutralQuality,
                MinimumQuality,
                MaximumQuality);

            CraftingQualityEffectResult[] effects = (source.QualityEffects ?? [])
                .Where(effect => !string.IsNullOrWhiteSpace(effect.Stat))
                .Select(effect => CalculateEffect(effect, quality))
                .ToArray();

            ingredients.Add(new CraftingIngredientQuality(
                key,
                source.Name?.Trim() ?? option?.Name?.Trim() ?? "Material",
                source.QuantityScu ?? option?.QuantityScu,
                option?.Unit?.Trim().ToLowerInvariant() ?? "scu",
                quality,
                effects));

            foreach (CraftingQualityEffectResult effect in effects)
            {
                double rawPercentage = effect.LowerIsBetter
                    ? -effect.Percentage
                    : effect.Percentage;
                double modifier = 1d + rawPercentage / 100d;
                if (combined.TryGetValue(effect.Stat, out CombinedEffect? existing))
                {
                    existing.Modifier *= modifier;
                }
                else
                {
                    combined[effect.Stat] = new CombinedEffect(modifier, effect.LowerIsBetter);
                }
            }
        }

        CraftingStatSummary[] summaries = combined
            .Select(pair => BuildSummary(pair.Key, pair.Value, blueprint.ItemStats))
            .ToArray();
        return new CraftingQualityCalculation(ingredients, summaries);
    }

    public static string BuildIngredientKey(CraftingIngredient ingredient, int index)
    {
        string slot = ingredient.Slot?.Trim() ?? string.Empty;
        return slot.Length > 0 ? $"{slot}#{index}" : $"ingredient#{index}";
    }

    private static CraftingQualityEffectResult CalculateEffect(
        CraftingQualityEffect effect,
        int quality)
    {
        int minimum = effect.QualityMinimum ?? MinimumQuality;
        int maximum = effect.QualityMaximum ?? MaximumQuality;
        double modifierAtMinimum = effect.ModifierAtMinimum ?? 1d;
        double modifierAtMaximum = effect.ModifierAtMaximum ?? 1d;
        double modifier = maximum == minimum
            ? modifierAtMinimum
            : modifierAtMinimum +
              (modifierAtMaximum - modifierAtMinimum) *
              ((quality - minimum) / (double)(maximum - minimum));
        double rawPercentage = (modifier - 1d) * 100d;
        bool lowerIsBetter = modifierAtMaximum < modifierAtMinimum;

        return new CraftingQualityEffectResult(
            effect.Stat?.Trim() ?? "Unknown",
            lowerIsBetter ? -rawPercentage : rawPercentage,
            lowerIsBetter);
    }

    private static CraftingStatSummary BuildSummary(
        string stat,
        CombinedEffect effect,
        CraftingItemStats? stats)
    {
        double rawPercentage = (effect.Modifier - 1d) * 100d;
        double displayPercentage = effect.LowerIsBetter
            ? -rawPercentage
            : rawPercentage;
        double? baseValue = ExtractBaseValue(stat, stats);
        if (baseValue is null)
        {
            return new CraftingStatSummary(
                stat,
                "—",
                $"{displayPercentage:+0;-0;+0}%",
                displayPercentage,
                effect.LowerIsBetter);
        }

        double crafted = baseValue.Value * effect.Modifier;
        return new CraftingStatSummary(
            stat,
            FormatNumber(baseValue.Value),
            FormatNumber(crafted),
            displayPercentage,
            effect.LowerIsBetter);
    }

    private static double? ExtractBaseValue(string stat, CraftingItemStats? stats) =>
        stat.Trim().ToLowerInvariant() switch
        {
            "impact force" => stats?.FireModes?.FirstOrDefault()?.DamageMultiplier,
            "fire rate" => stats?.FireModes?.FirstOrDefault()?.FireRate,
            _ => null
        };

    private static string FormatNumber(double value) =>
        Math.Abs(value - Math.Round(value)) < 0.0000001d
            ? Math.Round(value).ToString("0")
            : value.ToString("0.00");

    private sealed class CombinedEffect(double modifier, bool lowerIsBetter)
    {
        public double Modifier { get; set; } = modifier;
        public bool LowerIsBetter { get; } = lowerIsBetter;
    }
}
