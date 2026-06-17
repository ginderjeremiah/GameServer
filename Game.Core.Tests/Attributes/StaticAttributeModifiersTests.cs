using Game.Core;
using Game.Core.Attributes.Modifiers;
using Xunit;

namespace Game.Core.Tests.Attributes
{
    /// <summary>
    /// Mirrors <c>UI/src/tests/lib/battle/attribute-modifier.test.ts</c> — the same canonical
    /// base/derived formulas, in the same backend apply order. <see cref="StaticAttributeModifiers.All"/>
    /// is the single source the frontend <c>STATIC_ATTRIBUTE_MODIFIERS</c> table is generated from, so
    /// both suites pin identical numbers to guard the cross-implementation parity.
    /// </summary>
    public class StaticAttributeModifiersTests
    {
        [Fact]
        public void All_ContainsTheCanonicalModifiers_InApplyOrder()
        {
            var expected = new (EAttribute Attribute, double Amount, EModifierType Type, EAttributeModifierSource Source, EAttribute? DerivedSource)[]
            {
                // CooldownRecovery = 1 (base) + 0.004·Agility + 0.001·Dexterity
                (EAttribute.CooldownRecovery, 1.0, EModifierType.Additive, EAttributeModifierSource.BaseValue, null),
                (EAttribute.CooldownRecovery, 0.004, EModifierType.Additive, EAttributeModifierSource.Derived, EAttribute.Agility),
                (EAttribute.CooldownRecovery, 0.001, EModifierType.Additive, EAttributeModifierSource.Derived, EAttribute.Dexterity),
                // Defense = 2 (base) + 1·Endurance + 0.5·Agility
                (EAttribute.Defense, 2.0, EModifierType.Additive, EAttributeModifierSource.BaseValue, null),
                (EAttribute.Defense, 1.0, EModifierType.Additive, EAttributeModifierSource.Derived, EAttribute.Endurance),
                (EAttribute.Defense, 0.5, EModifierType.Additive, EAttributeModifierSource.Derived, EAttribute.Agility),
                // MaxHealth = 50 (base) + 20·Endurance + 5·Strength
                (EAttribute.MaxHealth, 50.0, EModifierType.Additive, EAttributeModifierSource.BaseValue, null),
                (EAttribute.MaxHealth, 20.0, EModifierType.Additive, EAttributeModifierSource.Derived, EAttribute.Endurance),
                (EAttribute.MaxHealth, 5.0, EModifierType.Additive, EAttributeModifierSource.Derived, EAttribute.Strength),
            };

            Assert.Equal(expected.Length, StaticAttributeModifiers.All.Count);
            foreach (var (modifier, want) in StaticAttributeModifiers.All.Zip(expected))
            {
                Assert.Equal(want.Attribute, modifier.Attribute);
                Assert.Equal(want.Amount, modifier.Amount);
                Assert.Equal(want.Type, modifier.Type);
                Assert.Equal(want.Source, modifier.Source);
                if (want.DerivedSource is EAttribute derivedSource)
                {
                    Assert.Equal(derivedSource, modifier.DerivedSource);
                }
            }
        }

        [Fact]
        public void All_AreAdditive()
        {
            Assert.All(StaticAttributeModifiers.All, modifier => Assert.Equal(EModifierType.Additive, modifier.Type));
        }
    }
}
