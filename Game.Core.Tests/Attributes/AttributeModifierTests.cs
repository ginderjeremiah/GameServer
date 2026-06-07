using Game.Core.Attributes;
using Game.Core.Attributes.Modifiers;
using Xunit;

namespace Game.Core.Tests.Attributes
{
    public class AttributeModifierTests
    {
        // ── Non-derived sources use the raw Amount ──────────────────────────

        [Fact]
        public void Apply_Additive_AddsAmountToCurrentValue()
        {
            var modifier = MakeModifier(EModifierType.Additive, amount: 5.0);

            var result = modifier.Apply(10.0, EmptyStore());

            Assert.Equal(15.0, result);
        }

        [Fact]
        public void Apply_Multiplicative_MultipliesCurrentValueByAmount()
        {
            var modifier = MakeModifier(EModifierType.Multiplicative, amount: 2.0);

            var result = modifier.Apply(10.0, EmptyStore());

            Assert.Equal(20.0, result);
        }

        [Fact]
        public void Apply_AdditiveWithNegativeAmount_SubtractsFromCurrentValue()
        {
            var modifier = MakeModifier(EModifierType.Additive, amount: -3.0);

            var result = modifier.Apply(10.0, EmptyStore());

            Assert.Equal(7.0, result);
        }

        [Fact]
        public void Apply_MultiplicativeByZero_ReturnsZero()
        {
            var modifier = MakeModifier(EModifierType.Multiplicative, amount: 0.0);

            var result = modifier.Apply(10.0, EmptyStore());

            Assert.Equal(0.0, result);
        }

        // ── Derived sources scale the Amount by the source attribute ────────

        [Fact]
        public void Apply_AdditiveDerived_ScalesAmountByDerivedSourceValue()
        {
            // modifierAmount = Amount(3) * store[Strength](10) = 30, then additive onto 5.
            var modifier = MakeDerivedModifier(EModifierType.Additive, amount: 3.0, derivedSource: EAttribute.Strength);

            var result = modifier.Apply(5.0, StoreWith(EAttribute.Strength, 10.0));

            Assert.Equal(35.0, result);
        }

        [Fact]
        public void Apply_MultiplicativeDerived_ScalesAmountByDerivedSourceValue()
        {
            // modifierAmount = Amount(2) * store[Strength](10) = 20, then multiplicative onto 5.
            var modifier = MakeDerivedModifier(EModifierType.Multiplicative, amount: 2.0, derivedSource: EAttribute.Strength);

            var result = modifier.Apply(5.0, StoreWith(EAttribute.Strength, 10.0));

            Assert.Equal(100.0, result);
        }

        [Fact]
        public void Apply_AdditiveDerived_WhenDerivedSourceIsZero_LeavesCurrentValueUnchanged()
        {
            // The derived source has no contributing modifiers, so it resolves to 0 and the
            // additive amount becomes 0.
            var modifier = MakeDerivedModifier(EModifierType.Additive, amount: 3.0, derivedSource: EAttribute.Strength);

            var result = modifier.Apply(5.0, EmptyStore());

            Assert.Equal(5.0, result);
        }

        // ── Unsupported modifier type ───────────────────────────────────────

        [Fact]
        public void Apply_UnsupportedModifierType_ThrowsModifierTypeNotSupportedException()
        {
            // EModifierType only defines Additive(1) and Multiplicative(2); an out-of-range value
            // represents a programming error and must surface loudly rather than silently no-op.
            var modifier = MakeModifier((EModifierType)99, amount: 5.0);

            Assert.Throws<ModifierTypeNotSupportedException>(() => modifier.Apply(10.0, EmptyStore()));
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private static AttributeModifier MakeModifier(EModifierType type, double amount) => new()
        {
            Attribute = EAttribute.Strength,
            Amount = amount,
            Type = type,
            Source = EAttributeModifierSource.Item,
        };

        private static AttributeModifier MakeDerivedModifier(EModifierType type, double amount, EAttribute derivedSource) => new()
        {
            Attribute = EAttribute.Strength,
            Amount = amount,
            Type = type,
            Source = EAttributeModifierSource.Derived,
            DerivedSource = derivedSource,
        };

        private static AttributeCollection EmptyStore() => new([]);

        private static AttributeCollection StoreWith(EAttribute attribute, double value) => new(
        [
            new AttributeModifier
            {
                Attribute = attribute,
                Amount = value,
                Type = EModifierType.Additive,
                Source = EAttributeModifierSource.PlayerStatPoints,
            },
        ]);
    }
}
