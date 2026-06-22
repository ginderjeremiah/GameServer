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
            // A constant poison on the enemy (a base DamageTakenPerSecond) ticks it for 2/tick; the 50-HP
            // enemy dies after exactly 50 DoT damage (25 ticks). The player fires a 0-damage skill so a
            // SkillStats row exists to assert the DoT is NOT attributed to it.
            var player = MakeBattler([Stat(Strength, 0)], [DamageSkill(1, baseDamage: 0)]);
            var enemy = MakeBattler(Stat(Strength, 0), Stat(DamageTakenPerSecond, 50)); // MaxHealth 50, no skills

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
            // A constant poison on the player (a base DamageTakenPerSecond) ticks for 2/tick; the 50-HP
            // player (no damage skill) dies after 50 DoT.
            var player = MakeBattler(Stat(Strength, 0), Stat(DamageTakenPerSecond, 50));
            var enemy = MakeBattler(Stat(Strength, 0));

            var result = new BattleSimulator(player, enemy, seed: 0).Simulate();

            Assert.True(result.PlayerDied);
            Assert.Equal(50, result.Stats.PlayerDamageTaken);
            Assert.Equal(0, result.Stats.PlayerDamageDealt);
        }

        [Fact]
        public void HealOnPlayer_CountsTowardPlayerDamageHealed()
        {
            // A constant self-regen (a base HealthRegenPerSecond) heals 3/tick while the enemy chips 5/tick
            // (baseDamage 7 − Def 2); the player stays below MaxHealth so the full 3 is restored each tick.
            // Capped at 5 ticks → 15 healed.
            var player = MakeBattler(Stat(Strength, 0), Stat(HealthRegenPerSecond, 75));
            var enemy = MakeBattler([Stat(Strength, 0)], [DamageSkill(2, baseDamage: 7)]);

            var result = new BattleSimulator(player, enemy, seed: 0).Simulate(maxMs: 200);

            Assert.False(result.Victory);
            Assert.False(result.PlayerDied);
            Assert.Equal(15, result.Stats.PlayerDamageHealed); // 3 healed × 5 ticks
        }

        // ── End-of-tick ordering: heal saves from a lethal DoT tick (#1090) ──

        [Fact]
        public void ResolveDamageOverTime_HealOffsetsLethalDotTick_KeepsPlayerAlive()
        {
            // A 50-HP player takes 60 DoT this tick (more than its whole health) but an equal 60 heal applies
            // before the death check, restoring it to MaxHealth — so it survives a DoT that would otherwise kill.
            var player = MakeBattler(
                Stat(Strength, 0), Stat(DamageTakenPerSecond, 1500), Stat(HealthRegenPerSecond, 1500));
            var enemy = MakeBattler(Stat(Strength, 0));
            var context = new BattleContext(player, enemy, 40, new Mulberry32(0));

            context.ResolveDamageOverTime();

            Assert.False(player.IsDead);
            Assert.Equal(50, player.CurrentHealth); // 50 − 60 + 60, capped at MaxHealth
        }

        [Fact]
        public void ResolveDamageOverTime_HealOffsetsLethalDotTick_KeepsEnemyAlive()
        {
            // The enemy resolves first; its own heal applies before its death check, so a regen saves it from an
            // otherwise-lethal DoT tick exactly as it does for the player.
            var player = MakeBattler(Stat(Strength, 0));
            var enemy = MakeBattler(
                Stat(Strength, 0), Stat(DamageTakenPerSecond, 1500), Stat(HealthRegenPerSecond, 1500));
            var context = new BattleContext(player, enemy, 40, new Mulberry32(0));

            context.ResolveDamageOverTime();

            Assert.False(enemy.IsDead);
            Assert.Equal(50, enemy.CurrentHealth);
        }

        [Fact]
        public void ResolveDamageOverTime_HealBelowNetDamage_StillKillsButCountsTheHeal()
        {
            // The heal applies before the death check but cannot offset the whole tick: a 5-HP player takes 10
            // DoT and heals 4, dying at −1. The applied heal is still recorded toward PlayerDamageHealed.
            var player = MakeBattler(
                Stat(Strength, 0), Stat(DamageTakenPerSecond, 250), Stat(HealthRegenPerSecond, 100));
            player.TakeDamage(47); // Defense 2 → 45 damage → MaxHealth 50 → CurrentHealth 5
            var enemy = MakeBattler(Stat(Strength, 0));
            var context = new BattleContext(player, enemy, 40, new Mulberry32(0));

            context.ResolveDamageOverTime();

            Assert.True(player.IsDead);
            Assert.Equal(-1, player.CurrentHealth); // 5 − 10 + 4
            Assert.Equal(4, context.Stats.PlayerDamageHealed);
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

        private static Skill DamageSkill(int id, double baseDamage) => new()
        {
            Id = id,
            Name = $"Skill {id}",
            Description = "",
            Rarity = ERarity.Common,
            CooldownMs = 40,
            BaseDamage = baseDamage,
            DamageMultipliers = [],
            Effects = [],
        };
    }
}
