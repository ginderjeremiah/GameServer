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
