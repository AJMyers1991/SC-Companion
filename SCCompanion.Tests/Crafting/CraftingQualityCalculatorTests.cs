using SCCompanion.Data.Crafting;

namespace SCCompanion.Tests.Crafting;

[TestClass]
public sealed class CraftingQualityCalculatorTests
{
    [TestMethod]
    public void Calculate_FlipsLowerIsBetterEffectsForDisplay()
    {
        CraftingBlueprint blueprint = BlueprintWithEffects(
            new CraftingQualityEffect("Recoil", null, 0, 1000, 1.4, 0.6, "multiplicative"));
        var qualities = new Dictionary<string, int> { ["FRAME#0"] = 1000 };

        CraftingQualityCalculation result = CraftingQualityCalculator.Calculate(blueprint, qualities);

        Assert.AreEqual(40d, result.Ingredients[0].Effects[0].Percentage, 0.000001d);
        Assert.AreEqual("+40%", result.StatSummary[0].CraftedValue);
    }

    [TestMethod]
    public void Calculate_CombinesMatchingEffectsMultiplicatively()
    {
        CraftingQualityEffect effect =
            new("Impact Force", null, 0, 1000, 0.9, 1.1, "multiplicative");
        CraftingBlueprint blueprint = BlueprintWithEffects(effect, effect);
        var qualities = new Dictionary<string, int>
        {
            ["FRAME#0"] = 1000,
            ["CABLING#1"] = 1000
        };

        CraftingQualityCalculation result = CraftingQualityCalculator.Calculate(blueprint, qualities);

        Assert.AreEqual(21d, result.StatSummary.Single().Percentage, 0.000001d);
        Assert.AreEqual("1.21", result.StatSummary.Single().CraftedValue);
    }

    [TestMethod]
    public void Calculate_GlobalNeutralQualityProducesZeroEffect()
    {
        CraftingBlueprint blueprint = BlueprintWithEffects(
            new CraftingQualityEffect("Recoil", null, 0, 1000, 1.4, 0.6, "multiplicative"));

        CraftingQualityCalculation result = CraftingQualityCalculator.Calculate(blueprint);

        Assert.AreEqual(0d, result.Ingredients[0].Effects[0].Percentage, 0.000001d);
    }

    private static CraftingBlueprint BlueprintWithEffects(params CraftingQualityEffect[] effects)
    {
        CraftingIngredient[] ingredients = effects.Select((effect, index) =>
            new CraftingIngredient(
                index == 0 ? "FRAME" : "CABLING",
                null,
                index == 0 ? "Tungsten" : "Gold",
                0.04,
                [new CraftingIngredientOption(null, "Material", 0.04, 0, "scu")],
                [effect])).ToArray();
        return new CraftingBlueprint(
            1,
            "bp",
            "Test",
            null,
            "Weapons / Rifle",
            180,
            1,
            ingredients,
            [],
            new CraftingItemStats(
                "weapon",
                null,
                [new CraftingFireMode("Rapid", 600, null, null, null, null, 1, null)],
                null,
                null));
    }
}
