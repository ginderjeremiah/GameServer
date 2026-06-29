using Game.Core;
using Game.Core.Attributes;
using Xunit;

namespace Game.Core.Tests.Attributes
{
    public class DamageTypesTests
    {
        [Fact]
        public void LeafTypes_AreTheEightLeafTypes()
        {
            Assert.Equal(Enum.GetValues<EDamageType>(), DamageTypes.LeafTypes);
        }

        [Fact]
        public void Keys_AreTheTenKeysInCanonicalOrder()
        {
            EDamageTypeKey[] expected =
            [
                EDamageTypeKey.Physical, EDamageTypeKey.Fire, EDamageTypeKey.Water, EDamageTypeKey.Earth,
                EDamageTypeKey.Wind, EDamageTypeKey.Bleed, EDamageTypeKey.Poison, EDamageTypeKey.Burn,
                EDamageTypeKey.Elemental, EDamageTypeKey.Dot,
            ];

            Assert.Equal(expected, DamageTypes.Keys.Select(info => info.Key));
        }

        // The full taxonomy table (spike #1320 decision 3). The key order is fixed and parity-critical.
        public static TheoryData<EDamageType, EDamageTypeKey[]> AppliesCases() => new()
        {
            { EDamageType.Physical, [EDamageTypeKey.Physical] },
            { EDamageType.Fire, [EDamageTypeKey.Fire, EDamageTypeKey.Elemental] },
            { EDamageType.Water, [EDamageTypeKey.Water, EDamageTypeKey.Elemental] },
            { EDamageType.Earth, [EDamageTypeKey.Earth, EDamageTypeKey.Elemental] },
            { EDamageType.Wind, [EDamageTypeKey.Wind, EDamageTypeKey.Elemental] },
            { EDamageType.Bleed, [EDamageTypeKey.Bleed, EDamageTypeKey.Dot] },
            { EDamageType.Poison, [EDamageTypeKey.Poison, EDamageTypeKey.Dot] },
            { EDamageType.Burn, [EDamageTypeKey.Burn, EDamageTypeKey.Fire, EDamageTypeKey.Elemental, EDamageTypeKey.Dot] },
        };

        [Theory]
        [MemberData(nameof(AppliesCases))]
        public void Applies_ReturnsExactKeySetInFixedOrder(EDamageType type, EDamageTypeKey[] expected)
        {
            Assert.Equal(expected, DamageTypes.Applies(type));
        }

        [Theory]
        [MemberData(nameof(AppliesCases))]
        public void Applies_LeafTypesOwnKeyComesFirst(EDamageType type, EDamageTypeKey[] expected)
        {
            // Every leaf type's own same-named key leads its applicable set, before any cross-cutting category.
            Assert.Equal((int)type, (int)expected[0]);
            Assert.Equal(expected[0], DamageTypes.Applies(type)[0]);
        }

        [Fact]
        public void Applies_IsDefinedForEveryLeafType()
        {
            foreach (var type in Enum.GetValues<EDamageType>())
            {
                Assert.NotEmpty(DamageTypes.Applies(type));
            }
        }

        [Theory]
        [InlineData(EDamageTypeKey.Physical, EAttribute.PhysicalAmplification, EAttribute.PhysicalResistance)]
        [InlineData(EDamageTypeKey.Fire, EAttribute.FireAmplification, EAttribute.FireResistance)]
        [InlineData(EDamageTypeKey.Elemental, EAttribute.ElementalAmplification, EAttribute.ElementalResistance)]
        [InlineData(EDamageTypeKey.Dot, EAttribute.DotAmplification, EAttribute.DotResistance)]
        public void AttributesFor_ReturnsTheKeysAmpResistPair(EDamageTypeKey key, EAttribute amp, EAttribute resist)
        {
            Assert.Equal((amp, resist), DamageTypes.AttributesFor(key));
        }

        [Fact]
        public void AmplificationAttributes_MapAppliesKeysToTheirAmplificationAttribute()
        {
            // Burn exercises the multi-key case: Burn + Fire + Elemental + DoT amplification, in order.
            Assert.Equal(
                new[]
                {
                    EAttribute.BurnAmplification, EAttribute.FireAmplification,
                    EAttribute.ElementalAmplification, EAttribute.DotAmplification,
                },
                DamageTypes.AmplificationAttributes(EDamageType.Burn));
        }

        [Fact]
        public void ResistanceAttributes_MapAppliesKeysToTheirResistanceAttribute()
        {
            Assert.Equal(
                new[]
                {
                    EAttribute.BurnResistance, EAttribute.FireResistance,
                    EAttribute.ElementalResistance, EAttribute.DotResistance,
                },
                DamageTypes.ResistanceAttributes(EDamageType.Burn));
        }

        [Fact]
        public void AmpAndResistAttributes_TrackAppliesForEveryLeafType()
        {
            foreach (var type in Enum.GetValues<EDamageType>())
            {
                var keys = DamageTypes.Applies(type);
                Assert.Equal(keys.Select(k => DamageTypes.AttributesFor(k).Amplification), DamageTypes.AmplificationAttributes(type));
                Assert.Equal(keys.Select(k => DamageTypes.AttributesFor(k).Resistance), DamageTypes.ResistanceAttributes(type));
            }
        }

        [Fact]
        public void KeyForAttribute_RoundTripsEveryAmpAndResistAttribute()
        {
            foreach (var info in DamageTypes.Keys)
            {
                Assert.Equal(info.Key, DamageTypes.KeyForAttribute(info.Amplification));
                Assert.Equal(info.Key, DamageTypes.KeyForAttribute(info.Resistance));
            }
        }

        [Theory]
        [InlineData(EAttribute.Strength)]
        [InlineData(EAttribute.Toughness)]
        [InlineData(EAttribute.BleedDamagePerSecond)]
        public void KeyForAttribute_ReturnsNullForNonAmpResistAttributes(EAttribute attribute)
        {
            Assert.Null(DamageTypes.KeyForAttribute(attribute));
        }

        [Fact]
        public void EveryEnumAttribute_IsClassifiedConsistently()
        {
            // Exactly the 20 amp/resist attributes map to a key; everything else does not. Guards the
            // enum and the taxonomy from drifting apart.
            var keyed = Enum.GetValues<EAttribute>().Count(a => DamageTypes.KeyForAttribute(a) is not null);
            Assert.Equal(DamageTypes.Keys.Count * 2, keyed);
        }

        [Fact]
        public void DotAccumulators_AreTheThreeDotTypesInFixedOrder()
        {
            // The fixed iteration order the end-of-tick DoT phase folds the types in (a parity contract).
            Assert.Equal(
                new[]
                {
                    (EDamageType.Bleed, EAttribute.BleedDamagePerSecond),
                    (EDamageType.Poison, EAttribute.PoisonDamagePerSecond),
                    (EDamageType.Burn, EAttribute.BurnDamagePerSecond),
                },
                DamageTypes.DotAccumulators.Select(info => (info.Type, info.Accumulator)));
        }

        [Fact]
        public void DotTypeForAccumulator_RoundTripsEveryDotAccumulator()
        {
            foreach (var info in DamageTypes.DotAccumulators)
            {
                Assert.Equal(info.Type, DamageTypes.DotTypeForAccumulator(info.Accumulator));
            }
        }

        [Theory]
        [InlineData(EAttribute.Strength)]
        [InlineData(EAttribute.HealthRegenPerSecond)]
        [InlineData(EAttribute.FireResistance)]
        public void DotTypeForAccumulator_ReturnsNullForNonDotAccumulators(EAttribute attribute)
        {
            Assert.Null(DamageTypes.DotTypeForAccumulator(attribute));
        }

        [Fact]
        public void DotAccumulators_AreTheOnlyDotLeafTypes()
        {
            // Every DoT-category leaf type (Bleed/Poison/Burn) has exactly one accumulator; no direct type does.
            var accumulatorTypes = DamageTypes.DotAccumulators.Select(info => info.Type).ToHashSet();
            foreach (var type in Enum.GetValues<EDamageType>())
            {
                var isDot = DamageTypes.Applies(type).Contains(EDamageTypeKey.Dot);
                Assert.Equal(isDot, accumulatorTypes.Contains(type));
            }
        }
    }
}
