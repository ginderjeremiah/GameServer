using Game.Core;
using Game.Core.Skills;
using Xunit;

namespace Game.Core.Tests.Skills
{
    /// <summary>
    /// Coverage for <see cref="Skill.PrimaryDamageType"/> — the derived "the skill's type" accessor the
    /// display surfaces and the interim single-type direct hit read (spike #1343). Mirrored on the frontend
    /// by <c>primaryDamageType</c> in <c>$lib/battle/damage-types.ts</c>.
    /// </summary>
    public class SkillTests
    {
        [Fact]
        public void PrimaryDamageType_SinglePortion_IsThatPortionsType()
        {
            var skill = NewSkill([new SkillDamagePortion { Type = EDamageType.Fire, Weight = 1.0 }]);

            Assert.Equal(EDamageType.Fire, skill.PrimaryDamageType);
        }

        [Fact]
        public void PrimaryDamageType_PicksTheHighestWeightPortion()
        {
            var skill = NewSkill(
            [
                new SkillDamagePortion { Type = EDamageType.Physical, Weight = 0.4 },
                new SkillDamagePortion { Type = EDamageType.Fire, Weight = 0.6 },
            ]);

            Assert.Equal(EDamageType.Fire, skill.PrimaryDamageType);
        }

        [Fact]
        public void PrimaryDamageType_OnWeightTie_PicksTheFirstAuthoredPortion()
        {
            // Equal weights: the first portion in authored order wins (strict '>' comparison).
            var skill = NewSkill(
            [
                new SkillDamagePortion { Type = EDamageType.Water, Weight = 1.0 },
                new SkillDamagePortion { Type = EDamageType.Fire, Weight = 1.0 },
            ]);

            Assert.Equal(EDamageType.Water, skill.PrimaryDamageType);
        }

        [Fact]
        public void PrimaryDamageType_NoPortions_FallsBackToPhysical()
        {
            // A malformed (empty) portion set never throws on a display/battle read — it reads as Physical.
            var skill = NewSkill([]);

            Assert.Equal(EDamageType.Physical, skill.PrimaryDamageType);
        }

        private static Skill NewSkill(IReadOnlyList<SkillDamagePortion> portions) => new()
        {
            Id = 0,
            Name = "Test",
            BaseDamage = 1,
            CriticalChance = 0,
            Description = "",
            CooldownMs = 1000,
            DamagePortions = portions,
            DamageMultipliers = [],
            Effects = [],
        };
    }
}
