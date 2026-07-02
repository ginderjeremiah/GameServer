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
        public void ApplyDamageOverTime_ScalesPerSecondAttributeByTick_BypassingMitigation()
        {
            // Endurance 50 → Toughness 100, but damage-over-time ignores mitigation entirely.
            var battler = MakeBattler(Stat(Endurance, 50), Stat(BleedDamagePerSecond, 50));
            var startHealth = battler.CurrentHealth;

            var dealt = battler.ApplyDamageOverTime(40); // 50 * 40 / 1000 = 2

            Assert.Equal(2, dealt);
            Assert.Equal(startHealth - 2, battler.CurrentHealth);
        }

        [Fact]
        public void ApplyDamageOverTime_NoDotAuthored_DealsZero()
        {
            var battler = MakeBattler(Stat(Strength, 10));
            var startHealth = battler.CurrentHealth;

            var dealt = battler.ApplyDamageOverTime(40);

            Assert.Equal(0, dealt);
            Assert.Equal(startHealth, battler.CurrentHealth);
        }

        [Fact]
        public void ApplyDamageOverTime_AppliesTypeResistanceSampledLive()
        {
            // 50 BleedDamagePerSecond → 2/tick, halved by 0.5 BleedResistance (sampled live each tick) → 1.
            var battler = MakeBattler(Stat(Strength, 10), Stat(BleedDamagePerSecond, 50), Stat(BleedResistance, 0.5));
            var startHealth = battler.CurrentHealth;

            var dealt = battler.ApplyDamageOverTime(40);

            Assert.Equal(1, dealt);
            Assert.Equal(startHealth - 1, battler.CurrentHealth);
        }

        [Fact]
        public void ApplyDamageOverTime_NegativeResistance_AmplifiesAsVulnerability()
        {
            // A −1.0 BleedResistance doubles the incoming bleed tick (factor 1 − (−1) = 2): 2 → 4. Unclamped.
            var battler = MakeBattler(Stat(Strength, 10), Stat(BleedDamagePerSecond, 50), Stat(BleedResistance, -1.0));
            var startHealth = battler.CurrentHealth;

            var dealt = battler.ApplyDamageOverTime(40);

            Assert.Equal(4, dealt);
            Assert.Equal(startHealth - 4, battler.CurrentHealth);
        }

        [Fact]
        public void ApplyDamageOverTime_ResistanceAboveOne_HealsAsAbsorption()
        {
            // A +2.0 BleedResistance drives the tick negative (2 × (1 − 2) = −2): absorption heals. DoT bypasses
            // mitigation and is not floored, so the negative tick restores health (here the battler is below max).
            var battler = MakeBattler(Stat(Strength, 10), Stat(BleedDamagePerSecond, 50), Stat(BleedResistance, 2.0));
            battler.TakeDamage(50, EDamageType.Physical, attackerLevel: 1); // no Toughness → 50 damage → CurrentHealth 50

            var dealt = battler.ApplyDamageOverTime(40);

            Assert.Equal(-2, dealt);
            Assert.Equal(52, battler.CurrentHealth); // 50 − (−2)
        }

        [Fact]
        public void ApplyDamageOverTime_BurnResistedByFireResistance_ThroughCrossCuttingKeys()
        {
            // Burn resists as burn + fire + elemental + dot, so a fire-resistant battler mitigates burns for
            // free: 250 BurnDamagePerSecond → 10/tick, halved by 0.5 FireResistance → 5.
            var battler = MakeBattler(Stat(Strength, 10), Stat(BurnDamagePerSecond, 250), Stat(FireResistance, 0.5));
            var startHealth = battler.CurrentHealth;

            var dealt = battler.ApplyDamageOverTime(40);

            Assert.Equal(5, dealt);
            Assert.Equal(startHealth - 5, battler.CurrentHealth);
        }

        [Fact]
        public void ApplyDamageOverTime_SumsEveryDotTypeInFixedOrder()
        {
            // All three accumulators tick together: Bleed 50→2, Poison 100→4, Burn 25→1 = 7 total, each typed
            // resistance sampled independently (none authored here). DotResistance 0.5 would resist all three.
            var battler = MakeBattler(
                Stat(Strength, 10),
                Stat(BleedDamagePerSecond, 50),
                Stat(PoisonDamagePerSecond, 100),
                Stat(BurnDamagePerSecond, 25));
            var startHealth = battler.CurrentHealth;

            var dealt = battler.ApplyDamageOverTime(40);

            Assert.Equal(7, dealt);
            Assert.Equal(startHealth - 7, battler.CurrentHealth);
        }

        [Fact]
        public void ApplyDamageOverTime_DotResistanceMitigatesEveryDotType()
        {
            // The DoT cross-cutting category resists all three types at once: Bleed 2, Poison 4, Burn 1 each ×
            // (1 − 0.5) → 1 + 2 + 0.5 = 3.5.
            var battler = MakeBattler(
                Stat(Strength, 10),
                Stat(BleedDamagePerSecond, 50),
                Stat(PoisonDamagePerSecond, 100),
                Stat(BurnDamagePerSecond, 25),
                Stat(DotResistance, 0.5));

            var dealt = battler.ApplyDamageOverTime(40);

            Assert.Equal(3.5, dealt);
        }

        [Fact]
        public void ApplyHealOverTime_ScalesPerSecondAttributeByTick()
        {
            var battler = MakeBattler(Stat(Strength, 10), Stat(HealthRegenPerSecond, 75)); // MaxHealth 100
            battler.TakeDamage(50, EDamageType.Physical, attackerLevel: 1); // no Toughness → 50 damage → CurrentHealth 50

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
            battler.TakeDamage(2, EDamageType.Physical, attackerLevel: 1); // no Toughness → 2 damage → CurrentHealth 98

            var healed = battler.ApplyHealOverTime(40); // would heal 3, but only 2 of room remains

            Assert.Equal(2, healed);
            Assert.Equal(100, battler.CurrentHealth);
        }

        [Fact]
        public void ApplyHealOverTime_KeepsIsDeadInSync()
        {
            var battler = MakeBattler(Stat(Strength, 10), Stat(HealthRegenPerSecond, 75)); // MaxHealth 100
            battler.TakeDamage(50, EDamageType.Physical, attackerLevel: 1); // no Toughness → 50 damage → CurrentHealth 50

            battler.ApplyHealOverTime(40); // heals 3 → CurrentHealth 53

            Assert.Equal(battler.CurrentHealth <= 0, battler.IsDead);
            Assert.False(battler.IsDead);
        }

        // ── Statistics attribution (backend-only) ────────────────────────────

        [Fact]
        public void DotOnEnemy_CountsTowardPlayerDamageDealt_TypedButNotHighestAttackOrSkillStats()
        {
            // A constant poison on the enemy (a base BleedDamagePerSecond) ticks it for 2/tick; the 50-HP
            // enemy dies after exactly 50 DoT damage (25 ticks). The player fires a 0-damage skill so a
            // SkillStats row exists to assert the DoT is NOT attributed to it.
            var player = MakeBattler([Stat(Strength, 0)], [DamageSkill(1, baseDamage: 0)]);
            var enemy = MakeBattler(Stat(Strength, 0), Stat(BleedDamagePerSecond, 50)); // MaxHealth 50, no skills

            var result = new BattleSimulator(player, enemy, seed: 0).Simulate();

            Assert.True(result.Victory);
            Assert.Equal(50, result.Stats.PlayerDamageDealt);
            Assert.Equal(0, result.Stats.PlayerDamageTaken);
            // DoT is not a "single attack" and is not attributed to the sourcing skill (per-skill DoT deferred).
            Assert.Equal(0, result.Stats.HighestPlayerAttack);
            Assert.Equal(0, result.Stats.SkillStats[1].TotalDamage);
            // It is, however, type-routed into the offense book — the player dealt 50 Bleed damage (#1338).
            Assert.Equal(50, result.Stats.TypedDamageDealt[EDamageType.Bleed]);
        }

        [Fact]
        public void DotOnPlayer_CountsTowardPlayerDamageTaken()
        {
            // A constant poison on the player (a base BleedDamagePerSecond) ticks for 2/tick; the 50-HP
            // player (no damage skill) dies after 50 DoT.
            var player = MakeBattler(Stat(Strength, 0), Stat(BleedDamagePerSecond, 50));
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
                Stat(Strength, 0), Stat(BleedDamagePerSecond, 1500), Stat(HealthRegenPerSecond, 1500));
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
                Stat(Strength, 0), Stat(BleedDamagePerSecond, 1500), Stat(HealthRegenPerSecond, 1500));
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
                Stat(Strength, 0), Stat(BleedDamagePerSecond, 250), Stat(HealthRegenPerSecond, 100));
            player.TakeDamage(45, EDamageType.Physical, attackerLevel: 1); // no Toughness → 45 damage → MaxHealth 50 → CurrentHealth 5
            var enemy = MakeBattler(Stat(Strength, 0));
            var context = new BattleContext(player, enemy, 40, new Mulberry32(0));

            context.ResolveDamageOverTime();

            Assert.True(player.IsDead);
            Assert.Equal(-1, player.CurrentHealth); // 5 − 10 + 4
            Assert.Equal(4, context.Stats.PlayerDamageHealed);
        }

        // ── Pre-mitigation exposure recorder (incoming book, #1337) ──────────

        [Fact]
        public void ApplyDamageOverTime_RecordExposure_ReportsPreMitigationPerType()
        {
            // The recorder receives each DoT type's PRE-resistance tick (50 → 2) while the dealt value is the
            // post-resistance 1, so a resist never throttles the exposure training signal.
            var battler = MakeBattler(Stat(Strength, 10), Stat(BleedDamagePerSecond, 50), Stat(BleedResistance, 0.5));
            var exposure = new Dictionary<EDamageType, double>();

            var dealt = battler.ApplyDamageOverTime(40, (type, amount) => exposure[type] = amount);

            Assert.Equal(1, dealt);                       // post-resistance (mitigated)
            Assert.Equal(2, exposure[EDamageType.Bleed]); // pre-mitigation (exposure)
        }

        [Fact]
        public void ApplyDamageOverTime_RecordExposure_ReportsOnlyActiveTypes()
        {
            // Each authored DoT type reports its pre-mitigation tick; a type with a zero accumulator is skipped.
            var battler = MakeBattler(
                Stat(Strength, 10), Stat(BleedDamagePerSecond, 50), Stat(BurnDamagePerSecond, 250));
            var exposure = new Dictionary<EDamageType, double>();

            battler.ApplyDamageOverTime(40, (type, amount) => exposure[type] = amount);

            Assert.Equal(2, exposure[EDamageType.Bleed]); // 50 → 2
            Assert.Equal(10, exposure[EDamageType.Burn]); // 250 → 10
            Assert.False(exposure.ContainsKey(EDamageType.Poison));
        }

        [Fact]
        public void ApplyDamageOverTime_NoRecorder_DealsIdenticalDamage()
        {
            // The recorders are pure side channels: omitting them leaves the dealt damage (and health math) unchanged.
            var battler = MakeBattler(Stat(Strength, 10), Stat(BleedDamagePerSecond, 50), Stat(BleedResistance, 0.5));

            var dealt = battler.ApplyDamageOverTime(40);

            Assert.Equal(1, dealt);
        }

        // ── Resistance-mitigated recorder (resist-training split, #1454) ─────

        [Fact]
        public void ApplyDamageOverTime_RecordMitigated_ReportsResistanceBlockedPerType()
        {
            // DoT bypasses the Toughness curve entirely, so resistance is its only mitigation: the mitigated
            // recorder receives exactly the pre-mitigation tick's resistance-blocked slice (2 × 0.5 = 1), the
            // same amount the exposure/dealt gap already reflects.
            var battler = MakeBattler(Stat(Strength, 10), Stat(BleedDamagePerSecond, 50), Stat(BleedResistance, 0.5));
            var mitigated = new Dictionary<EDamageType, double>();

            var dealt = battler.ApplyDamageOverTime(40, recordMitigated: (type, amount) => mitigated[type] = amount);

            Assert.Equal(1, dealt);
            Assert.Equal(1, mitigated[EDamageType.Bleed]);
        }

        [Fact]
        public void ApplyDamageOverTime_RecordMitigated_NegativeResistanceClampsToZero()
        {
            // A negative resistance (vulnerability) is anti-mitigation, not resistance — it blocks nothing, so
            // the mitigated slice clamps to 0 rather than going negative.
            var battler = MakeBattler(Stat(Strength, 10), Stat(BleedDamagePerSecond, 50), Stat(BleedResistance, -1.0));
            var mitigated = new Dictionary<EDamageType, double>();

            battler.ApplyDamageOverTime(40, recordMitigated: (type, amount) => mitigated[type] = amount);

            Assert.Equal(0, mitigated[EDamageType.Bleed]);
        }

        // ── Post-mitigation damage-dealt recorder (offense book, #1338) ──────

        [Fact]
        public void ApplyDamageOverTime_RecordDamageDealt_ReportsPostMitigationPerType()
        {
            // The damage-dealt recorder receives each DoT type's POST-resistance tick (50 → 1), the same value
            // the tick subtracts from health — the attacker's offense trains on what it actually dealt, so a
            // victim's resistance legitimately discounts it (unlike the pre-mitigation exposure recorder).
            var battler = MakeBattler(Stat(Strength, 10), Stat(BleedDamagePerSecond, 50), Stat(BleedResistance, 0.5));
            var dealtByType = new Dictionary<EDamageType, double>();

            var dealt = battler.ApplyDamageOverTime(40, recordDamageDealt: (type, amount) => dealtByType[type] = amount);

            Assert.Equal(1, dealt);                          // post-resistance (mitigated)
            Assert.Equal(1, dealtByType[EDamageType.Bleed]); // recorded value matches the mitigated tick
        }

        [Fact]
        public void ApplyDamageOverTime_RecordDamageDealt_ReportsOnlyActiveTypes()
        {
            // Each authored DoT type reports its post-mitigation tick; a type with a zero accumulator is skipped.
            var battler = MakeBattler(
                Stat(Strength, 10), Stat(BleedDamagePerSecond, 50), Stat(BurnDamagePerSecond, 250));
            var dealtByType = new Dictionary<EDamageType, double>();

            battler.ApplyDamageOverTime(40, recordDamageDealt: (type, amount) => dealtByType[type] = amount);

            Assert.Equal(2, dealtByType[EDamageType.Bleed]); // 50 → 2 (no resistance)
            Assert.Equal(10, dealtByType[EDamageType.Burn]); // 250 → 10
            Assert.False(dealtByType.ContainsKey(EDamageType.Poison));
        }

        [Fact]
        public void ResolveDamageOverTime_RecordsPlayerDotExposure_AndEnemyDotAsDamageDealt()
        {
            // The two books split by side: the player's incoming DoT records pre-mitigation exposure into the
            // incoming book, while the enemy's DoT (the player's DoT damage dealt) records post-mitigation into
            // the offense book (#1338). Neither leaks into the other.
            var player = MakeBattler(Stat(Strength, 0), Stat(BurnDamagePerSecond, 250)); // player takes Burn
            var enemy = MakeBattler(Stat(Strength, 0), Stat(BleedDamagePerSecond, 50));  // enemy takes Bleed
            var context = new BattleContext(player, enemy, 40, new Mulberry32(0));

            context.ResolveDamageOverTime();

            Assert.Equal(10, context.Stats.TypedDamageExposure[EDamageType.Burn], 0.001); // 250 → 10 pre-mitigation
            Assert.False(context.Stats.TypedDamageExposure.ContainsKey(EDamageType.Bleed));
            Assert.Equal(2, context.Stats.TypedDamageDealt[EDamageType.Bleed], 0.001);     // 50 → 2 dealt to enemy
            Assert.False(context.Stats.TypedDamageDealt.ContainsKey(EDamageType.Burn));
            // No resistance authored on either side, so the player's incoming Burn blocks nothing (#1454).
            Assert.Equal(0, context.Stats.TypedDamageResistanceMitigated[EDamageType.Burn], 0.001);
        }

        [Fact]
        public void ResolveDamageOverTime_PlayerBurnResistance_RecordsResistanceMitigatedShare()
        {
            // The player's incoming DoT resist-mitigated tracking (#1454) flows through ResolveDamageOverTime
            // exactly like ApplyDamageOverTime's recorder: 250 BurnDamagePerSecond → 10 pre-mitigation, halved by
            // 0.5 FireResistance → 5 mitigated (and 5 actually dealt).
            var player = MakeBattler(Stat(Strength, 0), Stat(FireResistance, 0.5), Stat(BurnDamagePerSecond, 250));
            var enemy = MakeBattler(Stat(Strength, 0));
            var context = new BattleContext(player, enemy, 40, new Mulberry32(0));

            context.ResolveDamageOverTime();

            Assert.Equal(10, context.Stats.TypedDamageExposure[EDamageType.Burn], 0.001);
            Assert.Equal(5, context.Stats.TypedDamageResistanceMitigated[EDamageType.Burn], 0.001);
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
            DamagePortions = [new SkillDamagePortion { Type = EDamageType.Physical, Weight = 1.0 }],
            CooldownMs = 40,
            BaseDamage = baseDamage,
            CriticalChance = 0,
            DamageMultipliers = [],
            Effects = [],
        };
    }
}
