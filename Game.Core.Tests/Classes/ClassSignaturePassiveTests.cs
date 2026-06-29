using Game.Core;
using Game.Core.Attributes.Modifiers;
using Game.Core.Classes;
using Xunit;

namespace Game.Core.Tests.Classes
{
    /// <summary>
    /// Unit tests for <see cref="ClassSignaturePassive.GetModifier"/> — the resolution of a class's signature
    /// passive into a single <see cref="EAttributeModifierSource.Class"/> attribute modifier. The composition
    /// into the battler (and its frontend↔backend parity) is covered by
    /// <see cref="Attributes.ClassSignaturePassiveParityTests"/> and the <c>BattleSnapshot</c> tests; here the
    /// resolution math itself is pinned in isolation.
    /// </summary>
    public class ClassSignaturePassiveTests
    {
        [Fact]
        public void GetModifier_FlatPassive_TagsClassSourceAndIgnoresScaling()
        {
            var passive = new ClassSignaturePassive
            {
                Attribute = EAttribute.Strength,
                Amount = 7m,
                ScalingAttribute = null,
                ScalingAmount = 0m,
                ModifierType = EModifierType.Additive,
            };

            // A flat passive never reads the scaling resolver — it would throw if it did.
            var modifier = passive.GetModifier(_ => throw new InvalidOperationException("flat passive must not scale"));

            Assert.Equal(EAttribute.Strength, modifier.Attribute);
            Assert.Equal(7d, modifier.Amount);
            Assert.Equal(EModifierType.Additive, modifier.Type);
            Assert.Equal(EAttributeModifierSource.Class, modifier.Source);
        }

        [Fact]
        public void GetModifier_AttributeScaledPassive_AddsScaledTermToFlatAmount()
        {
            var passive = new ClassSignaturePassive
            {
                Attribute = EAttribute.Toughness,
                Amount = 2m,
                ScalingAttribute = EAttribute.Endurance,
                ScalingAmount = 0.5m,
                ModifierType = EModifierType.Additive,
            };

            // Amount + ScalingAmount × value(Endurance) = 2 + 0.5 × 15 = 9.5.
            var modifier = passive.GetModifier(attribute =>
            {
                Assert.Equal(EAttribute.Endurance, attribute);
                return 15d;
            });

            Assert.Equal(EAttribute.Toughness, modifier.Attribute);
            Assert.Equal(9.5d, modifier.Amount);
            Assert.Equal(EAttributeModifierSource.Class, modifier.Source);
        }

        [Fact]
        public void GetModifier_PreservesMultiplicativeType()
        {
            var passive = new ClassSignaturePassive
            {
                Attribute = EAttribute.MaxHealth,
                Amount = 1.5m,
                ScalingAttribute = null,
                ScalingAmount = 0m,
                ModifierType = EModifierType.Multiplicative,
            };

            var modifier = passive.GetModifier(_ => 0d);

            Assert.Equal(EModifierType.Multiplicative, modifier.Type);
            Assert.Equal(1.5d, modifier.Amount);
        }

        [Fact]
        public void GetModifier_FractionalScaling_UsesDoubleArithmetic()
        {
            var passive = new ClassSignaturePassive
            {
                Attribute = EAttribute.Luck,
                Amount = 0m,
                ScalingAttribute = EAttribute.Strength,
                ScalingAmount = 0.1m,
                ModifierType = EModifierType.Additive,
            };

            // (double)0.1m × 3 is 0.30000000000000004, not the decimal 0.3 — the exact value the frontend mirror
            // computes, so the bit-for-bit parity surface holds for a fractional scaling amount.
            var modifier = passive.GetModifier(_ => 3d);

            Assert.Equal(0.30000000000000004d, modifier.Amount);
        }
    }
}
