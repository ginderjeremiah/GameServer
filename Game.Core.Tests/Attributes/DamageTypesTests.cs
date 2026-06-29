using Game.Core;
using Game.Core.Attributes;
using Xunit;

namespace Game.Core.Tests.Attributes
{
    public class DamageTypesTests
    {
        [Fact]
        public void LeafTypes_AreEveryDamageTypeInEnumOrder()
        {
            Assert.Equal(Enum.GetValues<EDamageType>(), DamageTypes.LeafTypes);
        }

        [Fact]
        public void Keys_AreTheKeysInCanonicalOrder()
        {
            // The #1320 leaves, the cross-cutting categories, then the #1340 weapon leaves (append-only order).
            EDamageTypeKey[] expected =
            [
                EDamageTypeKey.Physical, EDamageTypeKey.Fire, EDamageTypeKey.Water, EDamageTypeKey.Earth,
                EDamageTypeKey.Wind, EDamageTypeKey.Bleed, EDamageTypeKey.Poison, EDamageTypeKey.Burn,
                EDamageTypeKey.Elemental, EDamageTypeKey.Dot,
                EDamageTypeKey.Sword, EDamageTypeKey.Axe, EDamageTypeKey.Bow, EDamageTypeKey.Club,
                EDamageTypeKey.Dagger, EDamageTypeKey.Unarmed,
            ];

            Assert.Equal(expected, DamageTypes.Keys.Select(info => info.Key));
        }

        // The full taxonomy table (spike #1320 decision 3, extended with the #1340 weapon leaves). The key order
        // is fixed and parity-critical. Each weapon leaf pulls its own key then the shared Physical key.
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
            { EDamageType.Sword, [EDamageTypeKey.Sword, EDamageTypeKey.Physical] },
            { EDamageType.Axe, [EDamageTypeKey.Axe, EDamageTypeKey.Physical] },
            { EDamageType.Bow, [EDamageTypeKey.Bow, EDamageTypeKey.Physical] },
            { EDamageType.Club, [EDamageTypeKey.Club, EDamageTypeKey.Physical] },
            { EDamageType.Dagger, [EDamageTypeKey.Dagger, EDamageTypeKey.Physical] },
            { EDamageType.Unarmed, [EDamageTypeKey.Unarmed, EDamageTypeKey.Physical] },
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
            // The key is matched by name rather than ordinal — the weapon keys were appended out of leaf-ordinal
            // alignment (append-only), so the original eight types' coincidental ordinal match no longer holds.
            Assert.Equal(Enum.GetName(type), Enum.GetName(expected[0]));
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
            Assert.Equal((amp, (EAttribute?)resist), DamageTypes.AttributesFor(key));
        }

        [Theory]
        [InlineData(EDamageTypeKey.Sword, EAttribute.SwordAmplification)]
        [InlineData(EDamageTypeKey.Axe, EAttribute.AxeAmplification)]
        [InlineData(EDamageTypeKey.Unarmed, EAttribute.UnarmedAmplification)]
        public void AttributesFor_WeaponKeysAreAmplificationOnly(EDamageTypeKey key, EAttribute amp)
        {
            // The #1340 weapon keys carry an amplification but no resistance.
            Assert.Equal((amp, (EAttribute?)null), DamageTypes.AttributesFor(key));
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
        public void WeaponLeaf_AmplifiesByWeaponAndPhysical_ButResistsByPhysicalOnly()
        {
            // A weapon hit amplifies via its own key + the shared Physical key, but the amplification-only weapon
            // key contributes no resistance, so mitigation rides PhysicalResistance alone (#1340).
            Assert.Equal(
                new[] { EAttribute.SwordAmplification, EAttribute.PhysicalAmplification },
                DamageTypes.AmplificationAttributes(EDamageType.Sword));
            Assert.Equal(
                new[] { EAttribute.PhysicalResistance },
                DamageTypes.ResistanceAttributes(EDamageType.Sword));
        }

        [Fact]
        public void AmpAndResistAttributes_TrackAppliesForEveryLeafType()
        {
            foreach (var type in Enum.GetValues<EDamageType>())
            {
                var keys = DamageTypes.Applies(type);
                Assert.Equal(keys.Select(k => DamageTypes.AttributesFor(k).Amplification), DamageTypes.AmplificationAttributes(type));
                // Amplification-only keys (the weapon leaves) contribute no resistance, so they drop out.
                Assert.Equal(keys.Select(k => DamageTypes.AttributesFor(k).Resistance).OfType<EAttribute>(), DamageTypes.ResistanceAttributes(type));
            }
        }

        [Fact]
        public void KeyForAttribute_RoundTripsEveryAmpAndResistAttribute()
        {
            foreach (var info in DamageTypes.Keys)
            {
                Assert.Equal(info.Key, DamageTypes.KeyForAttribute(info.Amplification));
                // A weapon key has no resistance attribute to round-trip.
                if (info.Resistance is EAttribute resistance)
                {
                    Assert.Equal(info.Key, DamageTypes.KeyForAttribute(resistance));
                }
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
            // Exactly the amp/resist attributes map to a key; everything else does not. Each key backs one
            // amplification, plus one resistance for the non-weapon keys (the weapon keys are amp-only). Guards
            // the enum and the taxonomy from drifting apart.
            var expected = DamageTypes.Keys.Count + DamageTypes.Keys.Count(info => info.Resistance is not null);
            var keyed = Enum.GetValues<EAttribute>().Count(a => DamageTypes.KeyForAttribute(a) is not null);
            Assert.Equal(expected, keyed);
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
