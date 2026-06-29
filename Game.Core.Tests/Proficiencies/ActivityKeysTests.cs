using Game.Core.Proficiencies;
using Xunit;

namespace Game.Core.Tests.Proficiencies
{
    /// <summary>
    /// The damage-key → activity-key routing maps (spike #1318 / #1338 / #1340). Each of the ten resist-bearing
    /// damage-type keys maps to exactly one offense key and one resist key; the amplification-only weapon keys
    /// (#1340) have an offense key but no resist key. Offense and resist are disjoint so the two books never
    /// collide when their folds share one accumulator in the accrual. The maps pin the correspondence explicitly
    /// rather than relying on the two enums keeping matching ordinals.
    /// </summary>
    public class ActivityKeysTests
    {
        public static TheoryData<EDamageTypeKey, EActivityKey, EActivityKey> KeyMappings => new()
        {
            { EDamageTypeKey.Physical, EActivityKey.Physical, EActivityKey.PhysicalResist },
            { EDamageTypeKey.Fire, EActivityKey.Fire, EActivityKey.FireResist },
            { EDamageTypeKey.Water, EActivityKey.Water, EActivityKey.WaterResist },
            { EDamageTypeKey.Earth, EActivityKey.Earth, EActivityKey.EarthResist },
            { EDamageTypeKey.Wind, EActivityKey.Wind, EActivityKey.WindResist },
            { EDamageTypeKey.Bleed, EActivityKey.Bleed, EActivityKey.BleedResist },
            { EDamageTypeKey.Poison, EActivityKey.Poison, EActivityKey.PoisonResist },
            { EDamageTypeKey.Burn, EActivityKey.Burn, EActivityKey.BurnResist },
            { EDamageTypeKey.Elemental, EActivityKey.Elemental, EActivityKey.ElementalResist },
            { EDamageTypeKey.Dot, EActivityKey.Dot, EActivityKey.DotResist },
        };

        // The weapon keys (#1340) are amplification-only: an offense key but no resist key.
        public static TheoryData<EDamageTypeKey, EActivityKey> WeaponKeyMappings => new()
        {
            { EDamageTypeKey.Sword, EActivityKey.Sword },
            { EDamageTypeKey.Axe, EActivityKey.Axe },
            { EDamageTypeKey.Bow, EActivityKey.Bow },
            { EDamageTypeKey.Club, EActivityKey.Club },
            { EDamageTypeKey.Dagger, EActivityKey.Dagger },
            { EDamageTypeKey.Unarmed, EActivityKey.Unarmed },
        };

        [Theory]
        [MemberData(nameof(KeyMappings))]
        public void ForDamageKey_MapsEachDamageKeyToItsOffenseAndResistKey(
            EDamageTypeKey damageKey, EActivityKey offense, EActivityKey resist)
        {
            Assert.Equal(offense, ActivityKeys.ForDamageKey(damageKey));
            Assert.Equal(resist, ActivityKeys.ForDamageKeyResist(damageKey));
        }

        [Theory]
        [MemberData(nameof(WeaponKeyMappings))]
        public void WeaponKeys_HaveAnOffenseKey_ButNoResistKey(EDamageTypeKey weaponKey, EActivityKey offense)
        {
            Assert.Equal(offense, ActivityKeys.ForDamageKey(weaponKey));
            Assert.Null(ActivityKeys.ForDamageKeyResist(weaponKey));
        }

        [Fact]
        public void OffenseAndResistKeys_AreDisjoint_AcrossEveryDamageKey()
        {
            // The shared accrual fold accumulates both books into one map, so an offense key colliding with a
            // resist key would cross-train the two axes. Guard the whole damage-key set against that. The
            // amplification-only weapon keys contribute an offense key but no resist key (filtered out below).
            var keys = Enum.GetValues<EDamageTypeKey>();
            var offense = keys.Select(ActivityKeys.ForDamageKey).ToHashSet();
            var resist = keys.Select(ActivityKeys.ForDamageKeyResist).OfType<EActivityKey>().ToHashSet();

            Assert.Empty(offense.Intersect(resist));
        }
    }
}
