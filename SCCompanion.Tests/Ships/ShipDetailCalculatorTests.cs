using SCCompanion.Data.Ships;

namespace SCCompanion.Tests.Ships;

[TestClass]
public sealed class ShipDetailCalculatorTests
{
    [TestMethod]
    public void BuildWeapons_NormalizesGroupsAndCountsNestedMissiles()
    {
        FleetYardsHardpoint[] hardpoints =
        [
            new() { Name = "hardpoint_weapon_gun_left", Group = "weapon", MinSize = 3 },
            new() { Name = "hardpoint_weapon_gun_left", Group = "weapon", MinSize = 3 },
            new()
            {
                Name = "hardpoint_missile_rack_left",
                Group = "weapon",
                Category = "missile_racks",
                MinSize = 4,
                Hardpoints =
                [
                    new FleetYardsHardpoint
                    {
                        Hardpoints =
                        [
                            new FleetYardsHardpoint { MinSize = 2 },
                            new FleetYardsHardpoint { MinSize = 2 }
                        ]
                    }
                ]
            }
        ];

        IReadOnlyList<ShipEquipmentSection> sections = ShipDetailCalculator.BuildWeaponSections(hardpoints);

        Assert.AreEqual("Weapons", sections[0].Heading);
        CollectionAssert.Contains(sections[0].Items.ToArray(), "2x Weapon Gun Left (S3)");
        Assert.AreEqual("Missiles", sections[1].Heading);
        CollectionAssert.Contains(sections[1].Items.ToArray(), "Missile Rack (S4) — 2x S2");
    }

    [TestMethod]
    public void BuildComponents_ExcludesInternalHardpointNamesAndGroupsDuplicates()
    {
        FleetYardsHardpoint[] hardpoints =
        [
            new() { Name = "Cooler", Group = "system", MinSize = 2 },
            new() { Name = "Cooler", Group = "system", MinSize = 2 },
            new() { Name = "hardpoint_internal", Group = "system", MinSize = 1 }
        ];

        IReadOnlyList<string> components = ShipDetailCalculator.BuildComponents(hardpoints);

        CollectionAssert.AreEqual(new[] { "2x Cooler (S2)" }, components.ToArray());
    }
}
