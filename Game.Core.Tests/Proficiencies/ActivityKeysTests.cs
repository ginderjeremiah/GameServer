using Game.Core.Proficiencies;
using Xunit;

namespace Game.Core.Tests.Proficiencies
{
    /// <summary>
    /// The damage-key → activity-key routing maps (spike #1318 / #1338). Each of the ten damage-type keys maps
    /// to exactly one offense key and one resist key; offense and resist are disjoint so the two books never
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

        [Theory]
        [MemberData(nameof(KeyMappings))]
        public void ForDamageKey_MapsEachDamageKeyToItsOffenseAndResistKey(
            EDamageTypeKey damageKey, EActivityKey offense, EActivityKey resist)
        {
            Assert.Equal(offense, ActivityKeys.ForDamageKey(damageKey));
            Assert.Equal(resist, ActivityKeys.ForDamageKeyResist(damageKey));
        }

        [Fact]
        public void OffenseAndResistKeys_AreDisjoint_AcrossEveryDamageKey()
        {
            // The shared accrual fold accumulates both books into one map, so an offense key colliding with a
            // resist key would cross-train the two axes. Guard the whole damage-key set against that.
            var keys = Enum.GetValues<EDamageTypeKey>();
            var offense = keys.Select(ActivityKeys.ForDamageKey).ToHashSet();
            var resist = keys.Select(ActivityKeys.ForDamageKeyResist).ToHashSet();

            Assert.Empty(offense.Intersect(resist));
        }
    }
}
