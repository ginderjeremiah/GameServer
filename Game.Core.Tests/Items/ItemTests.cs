using Game.Core.Attributes.Modifiers;
using Game.Core.Items;
using Xunit;

namespace Game.Core.Tests.Items
{
    public class ItemTests
    {
        [Fact]
        public void GetAttributeModifiers_NoAppliedMods_ReturnsOnlyItemAttributes()
        {
            var item = MakeItem(1, [MakeModifier(EAttribute.Strength, 5)]);

            var modifiers = item.GetAttributeModifiers([]).ToList();

            var modifier = Assert.Single(modifiers);
            Assert.Equal(EAttribute.Strength, modifier.Attribute);
            Assert.Equal(5, modifier.Amount);
        }

        [Fact]
        public void GetAttributeModifiers_WithAppliedMod_ConcatsItemThenModAttributes()
        {
            var item = MakeItem(1, [MakeModifier(EAttribute.Strength, 5)]);
            var mod = MakeMod(10, [MakeModifier(EAttribute.Dexterity, 7)]);

            var modifiers = item.GetAttributeModifiers([mod]).ToList();

            Assert.Equal(2, modifiers.Count);
            Assert.Equal(EAttribute.Strength, modifiers[0].Attribute);
            Assert.Equal(EAttribute.Dexterity, modifiers[1].Attribute);
        }

        [Fact]
        public void GetAttributeModifiers_MultipleMods_IncludesEveryModContribution()
        {
            var item = MakeItem(1, []);
            var modifiers = item.GetAttributeModifiers(
            [
                MakeMod(10, [MakeModifier(EAttribute.Strength, 3)]),
                MakeMod(11, [MakeModifier(EAttribute.Agility, 4)]),
            ]).ToList();

            Assert.Equal(2, modifiers.Count);
            Assert.Contains(modifiers, m => m.Attribute == EAttribute.Strength && m.Amount == 3);
            Assert.Contains(modifiers, m => m.Attribute == EAttribute.Agility && m.Amount == 4);
        }

        // ── MeetsProficiencyRequirement ─────────────────────────────────────

        [Fact]
        public void MeetsProficiencyRequirement_UngatedItem_AlwaysMet()
        {
            var item = MakeItem(1, []);

            Assert.True(item.MeetsProficiencyRequirement(new Dictionary<int, int>()));
            Assert.True(item.MeetsProficiencyRequirement(new Dictionary<int, int> { [3] = 0 }));
        }

        [Theory]
        [InlineData(5, true)]   // exactly the required level meets the gate
        [InlineData(6, true)]   // above the required level meets the gate
        [InlineData(4, false)]  // below the required level fails the gate
        public void MeetsProficiencyRequirement_GatedItem_ComparesAgainstPlayerLevel(int playerLevel, bool expected)
        {
            var item = MakeGatedItem(requiredProficiencyId: 3, requiredProficiencyLevel: 5);

            var met = item.MeetsProficiencyRequirement(new Dictionary<int, int> { [3] = playerLevel });

            Assert.Equal(expected, met);
        }

        [Fact]
        public void MeetsProficiencyRequirement_GatedItem_MissingProficiencyTreatedAsLevelZero()
        {
            var item = MakeGatedItem(requiredProficiencyId: 3, requiredProficiencyLevel: 1);

            // The player has never trained proficiency 3 (no entry), so they are below any positive gate.
            Assert.False(item.MeetsProficiencyRequirement(new Dictionary<int, int> { [9] = 99 }));
        }

        private static Item MakeItem(int id, List<AttributeModifier> attributes) => new()
        {
            Id = id,
            Name = $"Item {id}",
            Description = string.Empty,
            Category = EItemCategory.Accessory,
            Rarity = ERarity.Common,
            Attributes = attributes,
            ModSlots = [],
        };

        private static Item MakeGatedItem(int requiredProficiencyId, int requiredProficiencyLevel) => new()
        {
            Id = 1,
            Name = "Gated",
            Description = string.Empty,
            Category = EItemCategory.Accessory,
            Rarity = ERarity.Common,
            RequiredProficiencyId = requiredProficiencyId,
            RequiredProficiencyLevel = requiredProficiencyLevel,
            Attributes = [],
            ModSlots = [],
        };

        private static ItemMod MakeMod(int id, List<AttributeModifier> attributes) => new()
        {
            Id = id,
            Name = $"Mod {id}",
            Description = string.Empty,
            Type = EItemModType.Prefix,
            Rarity = ERarity.Common,
            Attributes = attributes,
        };

        private static AttributeModifier MakeModifier(EAttribute attribute, double amount) => new()
        {
            Attribute = attribute,
            Amount = amount,
            Type = EModifierType.Additive,
            Source = EAttributeModifierSource.Item,
        };
    }
}
