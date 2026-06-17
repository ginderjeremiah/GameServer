using Game.Core.Attributes;
using Game.Core.Attributes.Modifiers;
using Game.Core.Battle;
using Game.Core.Skills;
using Xunit;
using static Game.Core.EAttribute;
using static Game.Core.EModifierType;

namespace Game.Core.Tests.Battle
{
    /// <summary>
    /// Unit coverage for the per-tick damage/heal-over-time application on <see cref="Battler"/> and the
    /// statistics attribution of the end-of-tick DoT/HoT phase (<see cref="BattleSimulator"/>). The
    /// per-battler arithmetic mirrors the frontend suite
    /// <c>UI/src/tests/lib/battle/battler-dot.test.ts</c>; the statistics are backend-only (the frontend
    /// does not track battle statistics).
    /// </summary>
    public class BattleDamageOverTimeTests
    {
        // ── Per-tick application (mirrored on the frontend) ──────────────────

        [Fact]
        public void ApplyDamageOverTime_ScalesPerSecondAttributeByTick_BypassingDefense()
        {
            // Endurance 50 → Defense 52, but damage-over-time ignores Defense entirely.
            var battler = MakeBattler(Stat(Endurance, 50), Stat(DamageTakenPerSecond, 50));
            var startHealth = battler.CurrentHealth;

            var dealt = battler.ApplyDamageOverTime(40); // 50 * 40 / 1000 = 2

            Assert.Equal(2, dealt);
            Assert.Equal(startHealth - 2, battler.CurrentHealth);
        }

        [Fact]
        public void ApplyHealOverTime_ScalesPerSecondAttributeByTick()
        {
            var battler = MakeBattler(Stat(Strength, 10), Stat(HealthRegenPerSecond, 75)); // MaxHealth 100
            battler.TakeDamage(52); // Defense 2 → 50 damage → CurrentHealth 50

            var healed = battler.ApplyHealOverTime(40); // 75 * 40 / 1000 = 3

            Assert.Equal(3, healed);
            Assert.Equal(53, battler.CurrentHealth);
        }

        [Fact]
        public void ApplyHealOverTime_AtFullHealth_HealsNothing()
        {
            var battler = MakeBattler(Stat(Strength, 10), Stat(HealthRegenPerSecond, 75));

            var healed = battler.ApplyHealOverTime(40);

            Assert.Equal(0, healed);
            Assert.Equal(100, battler.CurrentHealth);
        }

        [Fact]
        public void ApplyHealOverTime_NearMax_HealsOnlyUpToTheCap()
        {
            var battler = MakeBattler(Stat(Strength, 10), Stat(HealthRegenPerSecond, 75)); // MaxHealth 100
            battler.TakeDamage(4); // Defense 2 → 2 damage → CurrentHealth 98

            var healed = battler.ApplyHealOverTime(40); // would heal 3, but only 2 of room remains

            Assert.Equal(2, healed);
            Assert.Equal(100, battler.CurrentHealth);
        }

        [Fact]
        public void ApplyHealOverTime_KeepsIsDeadInSync()
        {
            var battler = MakeBattler(Stat(Strength, 10), Stat(HealthRegenPerSecond, 75)); // MaxHealth 100
            battler.TakeDamage(52); // CurrentHealth 50

            battler.ApplyHealOverTime(40); // heals 3 → CurrentHealth 53

            Assert.Equal(battler.CurrentHealth <= 0, battler.IsDead);
            Assert.False(battler.IsDead);
        }

        // ── Statistics attribution (backend-only) ────────────────────────────

        [Fact]
        public void DotOnEnemy_CountsTowardPlayerDamageDealt_NotHighestAttackOrSkillStats()
        {
            // The player poisons the enemy for 2/tick with no direct damage; the 50-HP enemy dies after
            // exactly 50 DoT damage (25 ticks).
            var player = MakeBattler([Stat(Strength, 0)],
                [EffectSkill(1, baseDamage: 0, ESkillEffectTarget.Opponent, DamageTakenPerSecond, 50)]);
            var enemy = MakeBattler([Stat(Strength, 0)]); // MaxHealth 50, no skills

            var result = new BattleSimulator(player, enemy, seed: 0).Simulate();

            Assert.True(result.Victory);
            Assert.Equal(50, result.Stats.PlayerDamageDealt);
            Assert.Equal(0, result.Stats.PlayerDamageTaken);
            // DoT is not a "single attack" and is not attributed to the sourcing skill (per-skill DoT deferred).
            Assert.Equal(0, result.Stats.HighestPlayerAttack);
            Assert.Equal(0, result.Stats.SkillStats[1].TotalDamage);
        }

        [Fact]
        public void DotOnPlayer_CountsTowardPlayerDamageTaken()
        {
            // The enemy poisons the player for 2/tick; the 50-HP player (no damage skill) dies after 50 DoT.
            var player = MakeBattler([Stat(Strength, 0)]);
            var enemy = MakeBattler([Stat(Strength, 0)],
                [EffectSkill(1, baseDamage: 0, ESkillEffectTarget.Opponent, DamageTakenPerSecond, 50)]);

            var result = new BattleSimulator(player, enemy, seed: 0).Simulate();

            Assert.True(result.PlayerDied);
            Assert.Equal(50, result.Stats.PlayerDamageTaken);
            Assert.Equal(0, result.Stats.PlayerDamageDealt);
        }

        [Fact]
        public void HealOnPlayer_CountsTowardPlayerDamageHealed()
        {
            // The player self-heals 3/tick while the enemy chips 5/tick (baseDamage 7 − Def 2); the player
            // stays below MaxHealth so the full 3 is restored each tick. Capped at 5 ticks → 15 healed.
            var player = MakeBattler([Stat(Strength, 0)],
                [EffectSkill(1, baseDamage: 0, ESkillEffectTarget.Self, HealthRegenPerSecond, 75)]);
            var enemy = MakeBattler([Stat(Strength, 0)], [DamageSkill(2, baseDamage: 7)]);

            var result = new BattleSimulator(player, enemy, seed: 0).Simulate(maxMs: 200);

            Assert.False(result.Victory);
            Assert.False(result.PlayerDied);
            Assert.Equal(15, result.Stats.PlayerDamageHealed); // 3 healed × 5 ticks
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private static Battler MakeBattler(params AttributeModifier[] modifiers) =>
            new(new AttributeCollection(modifiers), [], 1);

        private static Battler MakeBattler(IEnumerable<AttributeModifier> modifiers, IEnumerable<Skill> skills) =>
            new(new AttributeCollection(modifiers), skills, 1);

        private static AttributeModifier Stat(EAttribute attribute, double amount) => new()
        {
            Attribute = attribute,
            Amount = amount,
            Type = Additive,
            Source = EAttributeModifierSource.PlayerStatPoints,
        };

        /// <summary>A skill firing every tick that applies a permanent per-second-attribute effect to a target.</summary>
        private static Skill EffectSkill(
            int id, double baseDamage, ESkillEffectTarget target, EAttribute attribute, double amount) => new()
            {
                Id = id,
                Name = $"Skill {id}",
                Description = "",
                CooldownMs = 40,
                BaseDamage = baseDamage,
                DamageMultipliers = [],
                Effects =
                [
                    new SkillEffect
                    {
                        Id = id * 10,
                        Target = target,
                        AttributeId = attribute,
                        ModifierType = Additive,
                        Amount = amount,
                        DurationMs = 1_000_000,
                    },
                ],
            };

        private static Skill DamageSkill(int id, double baseDamage) => new()
        {
            Id = id,
            Name = $"Skill {id}",
            Description = "",
            CooldownMs = 40,
            BaseDamage = baseDamage,
            DamageMultipliers = [],
            Effects = [],
        };
    }
}
