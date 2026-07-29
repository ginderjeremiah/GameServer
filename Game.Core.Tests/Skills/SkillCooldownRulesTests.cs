using Game.Core;
using Game.Core.Skills;
using Xunit;

namespace Game.Core.Tests.Skills
{
    /// <summary>
    /// The authoring floor on <see cref="Skill.CooldownMs"/>. The rule is a pure predicate so the admin save
    /// guard, the content-health lint, and the Workbench all price the boundary identically.
    /// </summary>
    public class SkillCooldownRulesTests
    {
        [Fact]
        public void MinSkillCooldownMs_IsOneSimulationTick()
        {
            // The floor is the tick, not an arbitrary constant: the engine charges by MsPerTick and resets on
            // fire, so it can never fire a skill faster than once per tick, while CombatRating divides by the
            // authored cooldown. Anything below a tick makes the two disagree.
            Assert.Equal(GameConstants.MsPerTick, SkillCooldownRules.MinSkillCooldownMs);
        }

        [Theory]
        [InlineData(GameConstants.MsPerTick)]
        [InlineData(GameConstants.MsPerTick + 1)]
        [InlineData(1000)]
        [InlineData(int.MaxValue)]
        public void IsValidCooldown_AtOrAboveTheFloor_IsValid(int cooldownMs)
        {
            Assert.True(SkillCooldownRules.IsValidCooldown(cooldownMs));
        }

        [Theory]
        [InlineData(GameConstants.MsPerTick - 1)]
        [InlineData(1)]
        [InlineData(0)]
        [InlineData(-1)]
        public void IsValidCooldown_BelowTheFloor_IsInvalid(int cooldownMs)
        {
            Assert.False(SkillCooldownRules.IsValidCooldown(cooldownMs));
        }
    }
}
