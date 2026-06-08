using Game.Core.Attributes;
using Game.Core.Attributes.Modifiers;
using Xunit;

namespace Game.Core.Tests.Attributes
{
    /// <summary>
    /// Parity guard for the shared derived-stat formulas. These cases mirror,
    /// with identical inputs and identical expected values, the
    /// "derived stat computation" block of the frontend suite
    /// <c>UI/src/tests/lib/battle/battle-attributes.test.ts</c>.
    /// The formula constants live in two hand-maintained places —
    /// <see cref="StaticAttributeModifiers"/> on the backend and
    /// <c>STATIC_ATTRIBUTE_MODIFIERS</c> on the frontend — and the battle
    /// simulation on both sides depends on them agreeing, so these assertions
    /// exist to make any silent drift between the two fail a build.
    /// </summary>
    public class BattleAttributesParityTests
    {
        [Fact]
        public void MaxHealth_Is50Plus20EndurancePlus5Strength()
        {
            var collection = MakeCollection(
                (EAttribute.Strength, 10),
                (EAttribute.Endurance, 20));

            Assert.Equal(50 + 20 * 20 + 5 * 10, collection[EAttribute.MaxHealth]);
        }

        [Fact]
        public void Defense_Is2PlusEndurancePlusHalfAgility()
        {
            var collection = MakeCollection(
                (EAttribute.Endurance, 30),
                (EAttribute.Agility, 20));

            Assert.Equal(2 + 30 + 0.5 * 20, collection[EAttribute.Defense]);
        }

        [Fact]
        public void CooldownRecovery_IsFourTenthsAgilityPlusOneTenthDexterity()
        {
            var collection = MakeCollection(
                (EAttribute.Agility, 20),
                (EAttribute.Dexterity, 10));

            Assert.Equal(0.4 * 20 + 0.1 * 10, collection[EAttribute.CooldownRecovery]);
        }

        [Fact]
        public void ZeroBaseStats_StillHaveDerivedBaseValues()
        {
            var collection = MakeCollection();

            Assert.Equal(50, collection[EAttribute.MaxHealth]);
            Assert.Equal(2, collection[EAttribute.Defense]);
            Assert.Equal(0, collection[EAttribute.CooldownRecovery]);
        }

        private static AttributeCollection MakeCollection(params (EAttribute Attribute, double Amount)[] allocations)
        {
            var modifiers = allocations.Select(a => new AttributeModifier
            {
                Attribute = a.Attribute,
                Amount = a.Amount,
                Type = EModifierType.Additive,
                Source = EAttributeModifierSource.PlayerStatPoints,
            });

            return new AttributeCollection(modifiers);
        }
    }
}
