using Game.Core;
using Game.Core.Battle;
using Game.Core.Players;
using Game.Core.Skills;
using Game.Core.TestInfrastructure.Builders;
using Xunit;
using static Game.Core.EAttribute;

namespace Game.Core.Tests.Battle
{
    public class BattleContextTests
    {
        // ── RecordSkillUse ───────────────────────────────────────────────────

        [Fact]
        public void RecordSkillUse_FirstUse_CreatesSkillStatsEntry()
        {
            var context = MakeContext();

            context.RecordSkillUse(skillId: 5, damage: 30.0);

            Assert.Equal(1, context.Stats.PlayerSkillsUsed);
            var skill = Assert.Contains(5, (IReadOnlyDictionary<int, SkillStats>)context.Stats.SkillStats);
            Assert.Equal(1, skill.Uses);
            Assert.Equal(30.0, skill.TotalDamage);
            Assert.Equal(30.0, skill.HighestSingleAttack);
        }

        [Fact]
        public void RecordSkillUse_RepeatedUses_AccumulatesUsesAndTotalDamage()
        {
            var context = MakeContext();

            context.RecordSkillUse(skillId: 5, damage: 30.0);
            context.RecordSkillUse(skillId: 5, damage: 20.0);

            Assert.Equal(2, context.Stats.PlayerSkillsUsed);
            var skill = context.Stats.SkillStats[5];
            Assert.Equal(2, skill.Uses);
            Assert.Equal(50.0, skill.TotalDamage);
        }

        [Fact]
        public void RecordSkillUse_TracksHighestSingleAttackPerSkill()
        {
            var context = MakeContext();

            context.RecordSkillUse(skillId: 5, damage: 30.0);
            context.RecordSkillUse(skillId: 5, damage: 80.0);
            context.RecordSkillUse(skillId: 5, damage: 10.0);

            Assert.Equal(80.0, context.Stats.SkillStats[5].HighestSingleAttack);
        }

        [Fact]
        public void RecordSkillUse_DifferentSkills_TrackedSeparately()
        {
            var context = MakeContext();

            context.RecordSkillUse(skillId: 5, damage: 30.0);
            context.RecordSkillUse(skillId: 9, damage: 12.0);

            Assert.Equal(2, context.Stats.PlayerSkillsUsed);
            Assert.Equal(2, context.Stats.SkillStats.Count);
            Assert.Equal(30.0, context.Stats.SkillStats[5].TotalDamage);
            Assert.Equal(12.0, context.Stats.SkillStats[9].TotalDamage);
        }

        [Fact]
        public void RecordSkillUse_WhenEnemyIsActive_RecordsNothing()
        {
            var context = MakeContext();
            context.SwapActiveAndTargetBattlers(); // enemy becomes the active battler

            context.RecordSkillUse(skillId: 5, damage: 30.0);

            Assert.Equal(0, context.Stats.PlayerSkillsUsed);
            Assert.Empty(context.Stats.SkillStats);
        }

        // ── DamageTarget: player crit ────────────────────────────────────────

        [Fact]
        public void DamageTarget_PlayerCrit_MultipliesRawBeforeMitigation()
        {
            // CriticalChance 1 always succeeds; CriticalDamage is the base 1.5 + 0.5 = 2, read directly as the multiplier.
            var player = MakeBattlerWith((CriticalChance, 1), (CriticalDamage, 0.5));
            var enemy = MakeBattlerWith((Endurance, 0)); // MaxHealth 50, Toughness 0
            var context = new BattleContext(player, enemy, timeDelta: 0, new Mulberry32(0));

            context.DamageTarget(20, Single(EDamageType.Physical)); // crit ⇒ 20×2 = 40; no Toughness ⇒ 40 dealt

            Assert.Equal(50 - 40, enemy.CurrentHealth, 0.001);
            Assert.Equal(40, context.Stats.PlayerDamageDealt, 0.001);
        }

        [Fact]
        public void DamageTarget_PlayerNoCrit_DealsRawUnmitigated()
        {
            var player = MakeBattlerWith((CriticalChance, 0), (CriticalDamage, 2));
            var enemy = MakeBattlerWith((Endurance, 0)); // Toughness 0
            var context = new BattleContext(player, enemy, timeDelta: 0, new Mulberry32(0));

            context.DamageTarget(20, Single(EDamageType.Physical)); // no crit ⇒ 20, no Toughness ⇒ 20

            Assert.Equal(50 - 20, enemy.CurrentHealth, 0.001);
        }

        // ── DamageTarget: enemy attacking the player (dodge) ──────────────────

        [Fact]
        public void DamageTarget_PlayerDodgesEnemyHit_DealsZero()
        {
            var player = MakeBattlerWith((DodgeChance, 1));
            var enemy = MakeBattlerWith((Endurance, 0));
            var context = new BattleContext(player, enemy, timeDelta: 0, new Mulberry32(0));
            context.SwapActiveAndTargetBattlers(); // enemy attacks the player
            var before = player.CurrentHealth;

            context.DamageTarget(20, Single(EDamageType.Physical)); // dodged ⇒ 0

            Assert.Equal(before, player.CurrentHealth, 0.001);
            Assert.Equal(0, context.Stats.PlayerDamageTaken, 0.001);
        }

        [Fact]
        public void DamageTarget_EnemyAttacking_NeverCrits()
        {
            // The enemy carries a forced crit, but the roll is gated on the player attacking, so the enemy's
            // hit lands un-multiplied (20, not 40). The player has no Toughness, so it lands in full.
            var player = MakeBattlerWith((Endurance, 0));
            var enemy = MakeBattlerWith((CriticalChance, 1), (CriticalDamage, 2));
            var context = new BattleContext(player, enemy, timeDelta: 0, new Mulberry32(0));
            context.SwapActiveAndTargetBattlers();
            var before = player.CurrentHealth;

            context.DamageTarget(20, Single(EDamageType.Physical));

            Assert.Equal(before - 20, player.CurrentHealth, 0.001);
        }

        // ── DamageTarget: crit/dodge/block statistics ────────────────────────

        [Fact]
        public void DamageTarget_PlayerCrit_RecordsCritHitAndPostMitigationDamage()
        {
            var player = MakeBattlerWith((CriticalChance, 1), (CriticalDamage, 0.5)); // CriticalDamage 2
            var enemy = MakeBattlerWith((Endurance, 0)); // Toughness 0
            var context = new BattleContext(player, enemy, timeDelta: 0, new Mulberry32(0));

            context.DamageTarget(20, Single(EDamageType.Physical)); // 20×2 = 40, no Toughness ⇒ 40 dealt

            Assert.Equal(1, context.Stats.CriticalHits);
            // The player-facing statistic is the actual full crit damage dealt.
            Assert.Equal(40, context.Stats.CriticalDamageDealt, 0.001);
            // The Precision signal is the normalized marginal bonus: baseline 20, investment m−1 = 1, φ(1) = 0.5
            // ⇒ 20 × 0.5 = 10 (the crit added 20 over the vanilla hit, discounted to 10 by the saturation).
            Assert.Equal(10, context.Stats.CriticalBonusDealt, 0.001);
        }

        [Fact]
        public void DamageTarget_PlayerNoCrit_RecordsNoCritStatistics()
        {
            var player = MakeBattlerWith((CriticalChance, 0), (CriticalDamage, 2));
            var enemy = MakeBattlerWith((Endurance, 0));
            var context = new BattleContext(player, enemy, timeDelta: 0, new Mulberry32(0));

            context.DamageTarget(20, Single(EDamageType.Physical));

            Assert.Equal(0, context.Stats.CriticalHits);
            Assert.Equal(0, context.Stats.CriticalDamageDealt, 0.001);
            // A build that never crits trains Precision on nothing.
            Assert.Equal(0, context.Stats.CriticalBonusDealt, 0.001);
        }

        [Fact]
        public void DamageTarget_MinimallyInvestedCrit_BooksASmallMarginalBonus()
        {
            // Base CriticalDamage only (1.5, no invested bonus): m = 1.5, investment m−1 = 0.5, φ(0.5) = 1/3.
            // baseline 20 ⇒ bonus ≈ 6.667 — a token crit trains Precision far less than a committed one.
            var player = MakeBattlerWith((CriticalChance, 1)); // CriticalDamage 1.5 (base)
            var enemy = MakeBattlerWith((Endurance, 0));
            var context = new BattleContext(player, enemy, timeDelta: 0, new Mulberry32(0));

            context.DamageTarget(20, Single(EDamageType.Physical)); // 20 × 1.5 = 30 dealt

            Assert.Equal(30, context.Stats.CriticalDamageDealt, 0.001);
            Assert.Equal(20.0 * 0.5 / 1.5, context.Stats.CriticalBonusDealt, 0.001);
        }

        [Fact]
        public void DamageTarget_HeavilyInvestedCrit_SaturatesTowardOneBaselineHit()
        {
            // Heavy crit-damage investment: CriticalDamage 1.5 + 98.5 = 100, investment 99, φ(99) = 99/100 = 0.99.
            // baseline 20 ⇒ bonus 19.8, approaching one baseline hit (20) — the saturation ceiling — even though
            // the full crit dealt 2000. The signal is bounded so a monster crit cannot dwarf every other axis.
            var player = MakeBattlerWith((CriticalChance, 1), (CriticalDamage, 98.5));
            var enemy = MakeBattlerWith((Endurance, 0));
            var context = new BattleContext(player, enemy, timeDelta: 0, new Mulberry32(0));

            context.DamageTarget(20, Single(EDamageType.Physical)); // 20 × 100 = 2000 dealt

            Assert.Equal(2000, context.Stats.CriticalDamageDealt, 0.001);
            Assert.Equal(19.8, context.Stats.CriticalBonusDealt, 0.001);
        }

        [Fact]
        public void DamageTarget_PlayerDodge_RecordsDodgeAndPostMitigationDamageAvoided()
        {
            var player = MakeBattlerWith((DodgeChance, 1)); // Toughness 0
            var enemy = MakeBattlerWith((Endurance, 0));
            var context = new BattleContext(player, enemy, timeDelta: 0, new Mulberry32(0));
            context.SwapActiveAndTargetBattlers();

            context.DamageTarget(20, Single(EDamageType.Physical)); // would deal 20 (no Toughness), fully avoided

            Assert.Equal(1, context.Stats.AttacksDodged);
            Assert.Equal(20, context.Stats.DamageDodged, 0.001);
        }

        [Fact]
        public void DamageTarget_NormalEnemyHit_RecordsNoDodge()
        {
            var player = MakeBattlerWith((Endurance, 0)); // no dodge chance
            var enemy = MakeBattlerWith((Endurance, 0));
            var context = new BattleContext(player, enemy, timeDelta: 0, new Mulberry32(0));
            context.SwapActiveAndTargetBattlers();

            context.DamageTarget(20, Single(EDamageType.Physical));

            Assert.Equal(0, context.Stats.AttacksDodged);
            Assert.Equal(0, context.Stats.DamageDodged, 0.001);
        }

        // ── DamageTarget: RNG draw order ─────────────────────────────────────

        [Fact]
        public void DamageTarget_DrawsOncePerPlayerHit_OncePerEnemyHit()
        {
            // The draw count is a pure function of the fire sequence: one crit draw when the player attacks,
            // then one dodge draw when the enemy attacks (Block's second draw was retired, spike #1330).
            // Verified by comparing the shared stream's position against a reference advanced by exactly two
            // draws — independent of the seed.
            const uint seed = 12345u;
            var rng = new Mulberry32(seed);
            var player = MakeBattlerWith((Endurance, 0));
            var enemy = MakeBattlerWith((Endurance, 0));
            var context = new BattleContext(player, enemy, timeDelta: 0, rng);

            context.DamageTarget(5, Single(EDamageType.Physical));               // player attacking → 1 draw
            context.SwapActiveAndTargetBattlers();
            context.DamageTarget(5, Single(EDamageType.Physical));               // enemy attacking → 1 draw

            var reference = new Mulberry32(seed);
            reference.Next();
            reference.Next();
            Assert.Equal(reference.Next(), rng.Next());
        }

        // ── DamageTarget: deterministic damage reflection (spike #1330) ───────

        [Fact]
        public void DamageTarget_EnemyReflectsPlayerHit_DealsNetShareBackToPlayerBypassingMitigation()
        {
            // The enemy (defender) carries 0.5 DamageReflection. The player's 40-damage hit lands in full (the
            // enemy has no Toughness), and 40 × 0.5 = 20 is returned to the player — ignoring the player's own
            // Toughness, which would otherwise have reduced it.
            var player = MakeBattlerWith((Endurance, 50)); // Toughness 100 — must NOT mitigate the reflected hit
            var enemy = MakeBattlerWith((Endurance, 0), (DamageReflection, 0.5));
            var context = new BattleContext(player, enemy, timeDelta: 0, new Mulberry32(0));
            var playerBefore = player.CurrentHealth;

            var dealt = context.DamageTarget(40, Single(EDamageType.Physical)); // 40 to the enemy

            Assert.Equal(40, dealt, 0.001);
            Assert.Equal(playerBefore - 20, player.CurrentHealth, 0.001); // 40 × 0.5, unmitigated
            Assert.Equal(40, context.Stats.PlayerDamageDealt, 0.001);     // just the hit; reflection is not the player's damage dealt here
            Assert.Equal(20, context.Stats.PlayerDamageTaken, 0.001);     // the enemy reflected 20 onto the player
            Assert.Equal(0, context.Stats.PlayerReflectedDamageDealt, 0.001); // the enemy reflected, not the player
        }

        [Fact]
        public void DamageTarget_PlayerReflectsEnemyHit_DealsNetShareBackToEnemyAsPlayerDamageDealt()
        {
            // The player (defender of the enemy's attack) carries 0.4 DamageReflection. The enemy's 50-damage
            // hit lands in full (the player has no Toughness), and 50 × 0.4 = 20 is dealt back to the enemy —
            // booked as the player's damage dealt (the tank's offence-through-defence).
            var player = MakeBattlerWith((Endurance, 0), (DamageReflection, 0.4));
            var enemy = MakeBattlerWith((Endurance, 0));
            var context = new BattleContext(player, enemy, timeDelta: 0, new Mulberry32(0));
            context.SwapActiveAndTargetBattlers(); // enemy attacks the player
            var enemyBefore = enemy.CurrentHealth;

            context.DamageTarget(50, Single(EDamageType.Physical));

            Assert.Equal(enemyBefore - 20, enemy.CurrentHealth, 0.001); // 50 × 0.4 reflected onto the enemy
            Assert.Equal(20, context.Stats.PlayerDamageDealt, 0.001);
            Assert.Equal(20, context.Stats.PlayerReflectedDamageDealt, 0.001); // the dedicated Retribution signal (#1363)
            Assert.Equal(50, context.Stats.PlayerDamageTaken, 0.001); // the player still took the full hit
        }

        [Fact]
        public void DamageTarget_DodgedHit_ReflectsNothing()
        {
            // A dodge zeroes the hit, so there is no net damage to reflect even though the player has reflection.
            var player = MakeBattlerWith((DodgeChance, 1), (DamageReflection, 1.0));
            var enemy = MakeBattlerWith((Endurance, 0));
            var context = new BattleContext(player, enemy, timeDelta: 0, new Mulberry32(0));
            context.SwapActiveAndTargetBattlers();
            var enemyBefore = enemy.CurrentHealth;

            context.DamageTarget(50, Single(EDamageType.Physical));

            Assert.Equal(enemyBefore, enemy.CurrentHealth, 0.001);
            Assert.Equal(0, context.Stats.PlayerDamageDealt, 0.001);
        }

        [Fact]
        public void DamageTarget_AbsorbedHit_ReflectsNothing()
        {
            // The enemy both reflects AND absorbs Fire (resistance > 1 → net heal). Reflection guards on a
            // POSITIVE net, so an absorbed hit returns nothing — without the guard the negative net would reflect
            // as a heal to the attacker. A prior physical hit (itself reflected) drops the enemy below MaxHealth
            // so the absorption actually heals it (a genuinely negative net); the player's health is captured
            // AFTER that hit, so only the absorbed Fire hit is measured.
            var player = MakeBattlerWith((Endurance, 0));
            var enemy = MakeBattlerWith((Endurance, 0), (FireResistance, 2.0), (DamageReflection, 1.0));
            var context = new BattleContext(player, enemy, timeDelta: 0, new Mulberry32(0));
            context.DamageTarget(30, Single(EDamageType.Physical)); // enemy 50 → 20; the enemy reflects this 30 onto the player
            var playerBefore = player.CurrentHealth;

            context.DamageTarget(20, Single(EDamageType.Fire)); // 20 × (1 − 2) = −20, absorbed (enemy 20 → 40) — no reflection

            Assert.Equal(playerBefore, player.CurrentHealth, 0.001); // the absorbed hit reflected nothing onto the attacker
        }

        // ── DamageTarget: damage typing (amplification / resistance, #1320) ──

        [Fact]
        public void DamageTarget_AmplificationAndResistance_ApplyAsSeparateMultipliers()
        {
            // The attacker's amplification and the defender's resistance are SEPARATE budgets — × (1 + amp)
            // then × (1 − res) — not a single (1 + amp − res). With FireAmp 0.5 and FireResist 0.5 on a 40-damage
            // Fire hit: 40 × 1.5 = 60, × 0.5 = 30; no Toughness ⇒ 30. A combined budget would cancel to
            // × (1 + 0.5 − 0.5) = 40.
            var player = MakeBattlerWith((FireAmplification, 0.5));
            var enemy = MakeBattlerWith((Endurance, 0), (FireResistance, 0.5)); // Toughness 0, MaxHealth 50
            var context = new BattleContext(player, enemy, timeDelta: 0, new Mulberry32(0));

            context.DamageTarget(40, Single(EDamageType.Fire));

            Assert.Equal(50 - 30, enemy.CurrentHealth, 0.001);
            Assert.Equal(30, context.Stats.PlayerDamageDealt, 0.001);
        }

        [Fact]
        public void DamageTarget_FireAmplification_AlsoSummedFromElementalAmplification()
        {
            // applies(Fire) = { Fire, Elemental }, so a hit is amplified by the additive SUM of both keys.
            // FireAmp 0.3 + ElementalAmp 0.2 = 0.5 → 40 × 1.5 = 60; no Toughness ⇒ 60.
            var player = MakeBattlerWith((FireAmplification, 0.3), (ElementalAmplification, 0.2));
            var enemy = MakeBattlerWith((Endurance, 0));
            var context = new BattleContext(player, enemy, timeDelta: 0, new Mulberry32(0));

            context.DamageTarget(40, Single(EDamageType.Fire));

            Assert.Equal(50 - 60, enemy.CurrentHealth, 0.001);
        }

        [Fact]
        public void DamageTarget_ResistanceAboveOne_HealsTheTargetAndIgnoresMitigation()
        {
            // Absorption: FireResistance 2.0 drives the post-resistance hit negative (20 × (1 − 2) = −20), a net
            // heal — and the Toughness curve is NOT applied to an absorbed hit (a heal of 20). A physical hit
            // first brings the enemy below MaxHealth so the heal lands (the heal is capped at MaxHealth).
            var player = MakeBattlerWith((Endurance, 0));
            var enemy = MakeBattlerWith((Endurance, 0), (FireResistance, 2.0)); // Toughness 0, MaxHealth 50
            var context = new BattleContext(player, enemy, timeDelta: 0, new Mulberry32(0));
            context.DamageTarget(30, Single(EDamageType.Physical)); // 30 (no Toughness) → CurrentHealth 20

            context.DamageTarget(20, Single(EDamageType.Fire));

            // Healed 20 (mitigation ignored) → 40; the booked damage is 30 (physical) + (−20) (absorption) = 10.
            Assert.Equal(40, enemy.CurrentHealth, 0.001);
            Assert.Equal(10, context.Stats.PlayerDamageDealt, 0.001);
        }

        [Fact]
        public void DamageTarget_CritAmplifiesBeforeResistanceAndToughness()
        {
            // Order: amp (none) → crit (× 2) → resist (× 0.5) → Toughness curve. The enemy's Toughness 20 against
            // the level-1 player halves the post-resistance hit. A normal Fire hit (20 × 0.5 = 10, × 0.5 = 5)
            // deals only 5, but the crit (20 × 2 = 40, × 0.5 = 20, × 0.5 = 10) punches through for 10.
            var player = MakeBattlerWith((CriticalChance, 1), (CriticalDamage, 0.5)); // CriticalDamage 2.0
            var enemy = MakeBattlerWith((Endurance, 0), (Toughness, 20), (FireResistance, 0.5)); // Toughness 20, MaxHealth 50
            var context = new BattleContext(player, enemy, timeDelta: 0, new Mulberry32(0));

            context.DamageTarget(20, Single(EDamageType.Fire));

            Assert.Equal(50 - 10, enemy.CurrentHealth, 0.001);
        }

        // ── DamageTarget: typed damage dealt (offense book, #1337) ───────────

        [Fact]
        public void DamageTarget_PlayerHit_RecordsTypedDamageDealtMatchingPlayerDamageDealt()
        {
            var player = MakeBattlerWith((Endurance, 0));
            var enemy = MakeBattlerWith((Endurance, 0)); // Toughness 0
            var context = new BattleContext(player, enemy, timeDelta: 0, new Mulberry32(0));

            context.DamageTarget(20, Single(EDamageType.Fire)); // no Toughness ⇒ 20 dealt

            // The typed offense book sums the same post-mitigation figure each hit booked into PlayerDamageDealt.
            Assert.Equal(20, context.Stats.PlayerDamageDealt, 0.001);
            Assert.Equal(20, TypedDealt(context, EDamageType.Fire), 0.001);
        }

        [Fact]
        public void DamageTarget_PlayerHitsDifferentTypes_AccumulatedPerType()
        {
            var player = MakeBattlerWith((Endurance, 0));
            var enemy = MakeBattlerWith((Endurance, 0));
            var context = new BattleContext(player, enemy, timeDelta: 0, new Mulberry32(0));

            context.DamageTarget(20, Single(EDamageType.Fire));     // 20
            context.DamageTarget(10, Single(EDamageType.Fire));     // 10
            context.DamageTarget(30, Single(EDamageType.Physical)); // 30

            Assert.Equal(30, TypedDealt(context, EDamageType.Fire), 0.001); // 20 + 10
            Assert.Equal(30, TypedDealt(context, EDamageType.Physical), 0.001);
        }

        [Fact]
        public void DamageTarget_PlayerCrit_TypedDamageDealtUsesPostMitigationActual()
        {
            var player = MakeBattlerWith((CriticalChance, 1), (CriticalDamage, 0.5)); // CriticalDamage 2
            var enemy = MakeBattlerWith((Endurance, 0)); // Toughness 0
            var context = new BattleContext(player, enemy, timeDelta: 0, new Mulberry32(0));

            context.DamageTarget(20, Single(EDamageType.Physical)); // 20×2 = 40, no Toughness ⇒ 40

            Assert.Equal(40, TypedDealt(context, EDamageType.Physical), 0.001);
        }

        [Fact]
        public void DamageTarget_EnemyAttacking_RecordsNoTypedDamageDealt()
        {
            // The offense book is the player's; an enemy hit never adds to it (it feeds the incoming book).
            var player = MakeBattlerWith((Endurance, 0));
            var enemy = MakeBattlerWith((Endurance, 0));
            var context = new BattleContext(player, enemy, timeDelta: 0, new Mulberry32(0));
            context.SwapActiveAndTargetBattlers();

            context.DamageTarget(20, Single(EDamageType.Physical));

            Assert.Empty(context.Stats.TypedDamageDealt);
        }

        // ── DamageTarget: pre-mitigation typed exposure (incoming book, #1337) ─

        [Fact]
        public void DamageTarget_EnemyHit_RecordsExposureBeforeResistanceAndMitigation()
        {
            // Exposure is the pre-mitigation hit (40 Fire), captured before the player's FireResistance reduces
            // the damage actually taken (40 × 0.5 = 20; the player has no Toughness, so the curve passes it through).
            var player = MakeBattlerWith((Endurance, 0), (FireResistance, 0.5)); // Toughness 0
            var enemy = MakeBattlerWith((Endurance, 0));
            var context = new BattleContext(player, enemy, timeDelta: 0, new Mulberry32(0));
            context.SwapActiveAndTargetBattlers();

            context.DamageTarget(40, Single(EDamageType.Fire));

            Assert.Equal(20, context.Stats.PlayerDamageTaken, 0.001);
            Assert.Equal(40, Exposure(context, EDamageType.Fire), 0.001);
        }

        [Fact]
        public void DamageTarget_EnemyAmplifiedHit_ExposureReflectsAmplifiedPreMitigationValue()
        {
            // Exposure is captured after the attacker's amplification (it sizes the incoming hit) but before
            // the defender's resistance: enemy FireAmplification 0.5 → 40 × 1.5 = 60 exposure.
            var player = MakeBattlerWith((Endurance, 0));
            var enemy = MakeBattlerWith((Endurance, 0), (FireAmplification, 0.5));
            var context = new BattleContext(player, enemy, timeDelta: 0, new Mulberry32(0));
            context.SwapActiveAndTargetBattlers();

            context.DamageTarget(40, Single(EDamageType.Fire));

            Assert.Equal(60, Exposure(context, EDamageType.Fire), 0.001);
        }

        [Fact]
        public void DamageTarget_PlayerDodge_RecordsNoExposure()
        {
            // A dodged hit was evaded, not mitigated — it does not train the resist (incoming) book; its
            // avoided damage trains evasion instead.
            var player = MakeBattlerWith((DodgeChance, 1));
            var enemy = MakeBattlerWith((Endurance, 0));
            var context = new BattleContext(player, enemy, timeDelta: 0, new Mulberry32(0));
            context.SwapActiveAndTargetBattlers();

            context.DamageTarget(20, Single(EDamageType.Physical));

            Assert.Empty(context.Stats.TypedDamageExposure);
        }

        [Fact]
        public void DamageTarget_PlayerActive_RecordsNoExposure()
        {
            // The incoming book is the player's exposure; the player's own hits never add to it.
            var player = MakeBattlerWith((Endurance, 0));
            var enemy = MakeBattlerWith((Endurance, 0));
            var context = new BattleContext(player, enemy, timeDelta: 0, new Mulberry32(0));

            context.DamageTarget(20, Single(EDamageType.Physical));

            Assert.Empty(context.Stats.TypedDamageExposure);
        }

        // ── DamageTarget: multi-typed portion loop (#1343 / #1385) ───────────
        // A hit's raw is split across the skill's weighted portions; each portion runs the single-type pipeline
        // under its own type and the nets are summed. One crit decision multiplies every portion; one dodge
        // zeroes the whole hit; reflection runs once on the summed net. Per-portion typed books, whole-hit
        // stats from the sum. Mirrored in the frontend battle-step suite.

        [Fact]
        public void DamageTarget_MultiPortion_SplitsRawByWeightAndSumsPerPortionNet()
        {
            // Portions [Physical 60, Fire 40] of a raw-100 hit → 60 Physical + 40 Fire. The enemy resists Fire
            // 0.5 (Physical unresisted), no Toughness: 60 + 40×0.5 = 60 + 20 = 80 net.
            var player = MakeBattlerWith((Endurance, 0));
            var enemy = MakeBattlerWith((Endurance, 0), (FireResistance, 0.5)); // MaxHealth 50, Toughness 0
            var context = new BattleContext(player, enemy, timeDelta: 0, new Mulberry32(0));

            var dealt = context.DamageTarget(100, Portions((EDamageType.Physical, 60), (EDamageType.Fire, 40)));

            Assert.Equal(80, dealt, 0.001);
            Assert.Equal(80, context.Stats.PlayerDamageDealt, 0.001);
            Assert.Equal(50 - 80, enemy.CurrentHealth, 0.001);
            // Per-portion typed offense book: each portion's own post-mitigation net under its type.
            Assert.Equal(60, TypedDealt(context, EDamageType.Physical), 0.001);
            Assert.Equal(20, TypedDealt(context, EDamageType.Fire), 0.001);
        }

        [Fact]
        public void DamageTarget_MultiPortion_SingleCritMultipliesEveryPortion()
        {
            // CriticalChance 1, CriticalDamage 1.5 + 0.5 = 2. One crit draw scales BOTH portions: [Physical 50,
            // Fire 50] of raw 20 → 10 each, ×2 → 20 each = 40 net (a per-portion-only crit would give 30).
            var player = MakeBattlerWith((CriticalChance, 1), (CriticalDamage, 0.5));
            var enemy = MakeBattlerWith((Endurance, 0)); // MaxHealth 50, Toughness 0
            var context = new BattleContext(player, enemy, timeDelta: 0, new Mulberry32(0));

            var dealt = context.DamageTarget(20, Portions((EDamageType.Physical, 50), (EDamageType.Fire, 50)));

            Assert.Equal(40, dealt, 0.001);
            Assert.Equal(50 - 40, enemy.CurrentHealth, 0.001);
            // A crit is one hit; the crit damage stat uses the whole-hit summed net.
            Assert.Equal(1, context.Stats.CriticalHits);
            Assert.Equal(40, context.Stats.CriticalDamageDealt, 0.001);
            // The marginal bonus sums each portion's vanilla-hit baseline (10 + 10 = 20) before φ: investment
            // m−1 = 1, φ(1) = 0.5 ⇒ 20 × 0.5 = 10, so a multi-typed crit trains Precision as one marginal event.
            Assert.Equal(10, context.Stats.CriticalBonusDealt, 0.001);
        }

        [Fact]
        public void DamageTarget_MultiPortion_OneDrawPerFireRegardlessOfPortionCount()
        {
            // The per-fire draw count is independent of portion count: a 3-portion player fire then a 3-portion
            // enemy fire advance the shared stream by exactly two (one crit draw, one dodge draw).
            const uint seed = 4242u;
            var rng = new Mulberry32(seed);
            var player = MakeBattlerWith((Endurance, 0));
            var enemy = MakeBattlerWith((Endurance, 0));
            var context = new BattleContext(player, enemy, timeDelta: 0, rng);
            var threePortions = Portions((EDamageType.Physical, 1), (EDamageType.Fire, 1), (EDamageType.Water, 1));

            context.DamageTarget(9, threePortions);              // player attacking → 1 draw
            context.SwapActiveAndTargetBattlers();
            context.DamageTarget(9, threePortions);              // enemy attacking → 1 draw

            var reference = new Mulberry32(seed);
            reference.Next();
            reference.Next();
            Assert.Equal(reference.Next(), rng.Next());
        }

        [Fact]
        public void DamageTarget_MultiPortion_DodgeZeroesWholeHitAndSumsAvoidedNet()
        {
            // A single dodge zeroes the whole multi-typed hit. The avoided damage is the sum of each portion's
            // net: [Physical 50, Fire 50] of raw 40 → 20 each (player no resist/Toughness) → 40 avoided.
            var player = MakeBattlerWith((DodgeChance, 1));
            var enemy = MakeBattlerWith((Endurance, 0));
            var context = new BattleContext(player, enemy, timeDelta: 0, new Mulberry32(0));
            context.SwapActiveAndTargetBattlers();
            var before = player.CurrentHealth;

            var dealt = context.DamageTarget(40, Portions((EDamageType.Physical, 50), (EDamageType.Fire, 50)));

            Assert.Equal(0, dealt, 0.001);
            Assert.Equal(before, player.CurrentHealth, 0.001);
            Assert.Equal(0, context.Stats.PlayerDamageTaken, 0.001);
            Assert.Equal(1, context.Stats.AttacksDodged);
            Assert.Equal(40, context.Stats.DamageDodged, 0.001);
            Assert.Empty(context.Stats.TypedDamageExposure); // a dodge records no exposure
        }

        [Fact]
        public void DamageTarget_MultiPortion_RecordsPerPortionPreResistExposure()
        {
            // An undodged enemy multi-typed hit records each portion's pre-resistance exposure under its type:
            // [Physical 50, Fire 50] of raw 40 → 20 each exposure. The player resists Fire 0.5, so the net taken
            // is 20 (Physical) + 10 (Fire) = 30, but exposure is the pre-resist 20/20.
            var player = MakeBattlerWith((Endurance, 0), (FireResistance, 0.5));
            var enemy = MakeBattlerWith((Endurance, 0));
            var context = new BattleContext(player, enemy, timeDelta: 0, new Mulberry32(0));
            context.SwapActiveAndTargetBattlers();

            var dealt = context.DamageTarget(40, Portions((EDamageType.Physical, 50), (EDamageType.Fire, 50)));

            Assert.Equal(30, dealt, 0.001);
            Assert.Equal(30, context.Stats.PlayerDamageTaken, 0.001);
            Assert.Equal(20, Exposure(context, EDamageType.Physical), 0.001);
            Assert.Equal(20, Exposure(context, EDamageType.Fire), 0.001);
        }

        [Fact]
        public void DamageTarget_MultiPortion_ReflectsOnceOnSummedNet()
        {
            // Reflection runs once on the summed net, not per portion. The enemy reflects 0.5; the player's
            // [Physical 50, Fire 50] raw-40 hit deals 20 + 20 = 40 net, so 40 × 0.5 = 20 is returned to the
            // player (bypassing its own Toughness 100).
            var player = MakeBattlerWith((Endurance, 50)); // Toughness 100 — must NOT mitigate the reflected hit
            var enemy = MakeBattlerWith((Endurance, 0), (DamageReflection, 0.5));
            var context = new BattleContext(player, enemy, timeDelta: 0, new Mulberry32(0));
            var playerBefore = player.CurrentHealth;

            var dealt = context.DamageTarget(40, Portions((EDamageType.Physical, 50), (EDamageType.Fire, 50)));

            Assert.Equal(40, dealt, 0.001);
            Assert.Equal(playerBefore - 20, player.CurrentHealth, 0.001);
            Assert.Equal(20, context.Stats.PlayerDamageTaken, 0.001);
        }

        [Fact]
        public void DamageTarget_MultiPortion_AbsorbingPortionCapsHealAtRoomInFixedOrder()
        {
            // Per-portion absorption with the order-dependent heal cap. Portions [Physical 20, Fire 80] of raw
            // 100 against an enemy absorbing Fire (resistance 2.0) at full health: the Physical portion deals 20
            // (full 50 → 30, opening 20 room), then the Fire portion's −80 absorption heal is capped at that 20
            // room (back to 50). Net 20 + (−20) = 0 — the fixed Physical-first order is what lets the heal land.
            var player = MakeBattlerWith((Endurance, 0));
            var enemy = MakeBattlerWith((Endurance, 0), (FireResistance, 2.0)); // MaxHealth 50, Toughness 0
            var context = new BattleContext(player, enemy, timeDelta: 0, new Mulberry32(0));

            var dealt = context.DamageTarget(100, Portions((EDamageType.Physical, 20), (EDamageType.Fire, 80)));

            Assert.Equal(0, dealt, 0.001);
            Assert.Equal(50, enemy.CurrentHealth, 0.001); // 50 → 30 (Physical) → 50 (Fire heal capped at 20 room)
            Assert.Equal(20, TypedDealt(context, EDamageType.Physical), 0.001);
            Assert.Equal(-20, TypedDealt(context, EDamageType.Fire), 0.001);
        }

        [Fact]
        public void DamageTarget_SinglePortionNonUnitWeight_IsIdentityToSingleTypeHit()
        {
            // The reduce-to-single-portion identity holds for any single portion, not just weight 1: raw × w ÷ w
            // = raw. A lone Fire portion at weight 2 deals exactly the single-type 20 (no resistance authored).
            var player = MakeBattlerWith((Endurance, 0));
            var enemy = MakeBattlerWith((Endurance, 0)); // MaxHealth 50, Toughness 0
            var context = new BattleContext(player, enemy, timeDelta: 0, new Mulberry32(0));

            var dealt = context.DamageTarget(20, Portions((EDamageType.Fire, 2.0)));

            Assert.Equal(20, dealt, 0.001);
            Assert.Equal(50 - 20, enemy.CurrentHealth, 0.001);
            Assert.Equal(20, TypedDealt(context, EDamageType.Fire), 0.001);
        }

        // ── DamageTarget: Hex vulnerability tally (#1427) ────────────────────

        [Fact]
        public void DamageTarget_NoVulnerabilityApplied_BooksNoHexBonus()
        {
            // A hit against an un-debuffed enemy enables no vulnerability damage, so Hex trains on nothing.
            var player = MakeBattlerWith((Endurance, 0));
            var enemy = MakeBattlerWith((Endurance, 0));
            var context = new BattleContext(player, enemy, timeDelta: 0, new Mulberry32(0));

            context.DamageTarget(30, Single(EDamageType.Fire));

            Assert.Equal(0, context.Stats.HexBonusDealt, 0.001);
        }

        [Fact]
        public void DamageTarget_InnateEnemyVulnerability_TrainsNoHex()
        {
            // The enemy is innately vulnerable to Fire (−0.5 FireResistance from the start), but nothing the
            // player applied lowered it — Hex credits only player-applied vulnerability, so this trains nothing.
            var player = MakeBattlerWith((Endurance, 0));
            var enemy = MakeBattlerWith((Endurance, 0), (FireResistance, -0.5));
            var context = new BattleContext(player, enemy, timeDelta: 0, new Mulberry32(0));

            context.DamageTarget(30, Single(EDamageType.Fire)); // 30 × 1.5 = 45 dealt, but no applied vulnerability

            Assert.Equal(45, context.Stats.PlayerDamageDealt, 0.001);
            Assert.Equal(0, context.Stats.HexBonusDealt, 0.001);
        }

        [Fact]
        public void DamageTarget_AppliedVulnerability_BooksNormalizedMarginalBonus()
        {
            // The player debuffs the enemy's FireResistance by −0.5 (a vulnerability of v = 0.5), then hits for
            // raw 30. The un-hexed hit would deal 30; the hexed hit deals 45 — the vulnerability enabled 15,
            // discounted by 1/(1 + 0.5) to 10 (the marginal, saturated on the debuff strength).
            var player = MakeBattlerWith((Endurance, 0));
            var enemy = MakeBattlerWith((Endurance, 0));
            var context = new BattleContext(player, enemy, timeDelta: 0, new Mulberry32(0));
            context.ApplySkillEffect(Vulnerability(FireResistance, -0.5));

            context.DamageTarget(30, Single(EDamageType.Fire)); // 30 × (1 − (−0.5)) = 45 dealt

            Assert.Equal(45, context.Stats.PlayerDamageDealt, 0.001);
            Assert.Equal(30.0 * 0.5 / 1.5, context.Stats.HexBonusDealt, 0.001); // (45 − 30) / (1 + 0.5) = 10
        }

        [Fact]
        public void DamageTarget_HexBonus_IsFlatInEnemyBaseResistance()
        {
            // Same applied vulnerability (v = 0.5) against a soft enemy (innate 0 → live −0.5) and a resistant
            // enemy (innate 0.4 → live −0.1). The enabled damage is the pre-resistance amount × v, so Hex banks
            // the identical bonus either way — no resist-farming (the "D × v is flat in base resistance" rule).
            var softContext = MakeHexContext(innateFireResistance: 0);
            var resistantContext = MakeHexContext(innateFireResistance: 0.4);

            softContext.DamageTarget(30, Single(EDamageType.Fire));
            resistantContext.DamageTarget(30, Single(EDamageType.Fire));

            Assert.Equal(30.0 * 0.5 / 1.5, softContext.Stats.HexBonusDealt, 0.001);
            Assert.Equal(softContext.Stats.HexBonusDealt, resistantContext.Stats.HexBonusDealt, 0.001);
        }

        [Fact]
        public void DamageTarget_HeavilyInvestedVulnerability_SaturatesTowardOneBaselineHit()
        {
            // A crushing vulnerability (v = 10) enables 10× the pre-resistance damage, but the 1/(1 + v)
            // saturation bounds the tally at one baseline (pre-resistance) hit: 30 × 10 / 11 ≈ 27.27, approaching
            // 30 — a monster debuff cannot dwarf every other training axis (mirrors the crit-bonus ceiling).
            var player = MakeBattlerWith((Endurance, 0));
            var enemy = MakeBattlerWith((Endurance, 0));
            var context = new BattleContext(player, enemy, timeDelta: 0, new Mulberry32(0));
            context.ApplySkillEffect(Vulnerability(FireResistance, -10));

            context.DamageTarget(30, Single(EDamageType.Fire));

            Assert.Equal(30.0 * 10.0 / 11.0, context.Stats.HexBonusDealt, 0.001);
        }

        [Fact]
        public void DamageTarget_CritAndHex_BookIndependentBonuses()
        {
            // Crit and Hex are separate overlays. Hex is measured on the pre-crit vanilla hit, so a crit does not
            // inflate it: v = 0.5 on raw 30 still banks 10. Crit books its own marginal off the (hexed) non-crit
            // baseline 45: investment m−1 = 1, φ(1) = 0.5 ⇒ 45 × 0.5 = 22.5. Neither overlay balloons the other.
            var player = MakeBattlerWith((CriticalChance, 1), (CriticalDamage, 0.5)); // CriticalDamage 2
            var enemy = MakeBattlerWith((Endurance, 0));
            var context = new BattleContext(player, enemy, timeDelta: 0, new Mulberry32(0));
            context.ApplySkillEffect(Vulnerability(FireResistance, -0.5));

            context.DamageTarget(30, Single(EDamageType.Fire)); // hexed non-crit 45, crit ×2 = 90 dealt

            Assert.Equal(30.0 * 0.5 / 1.5, context.Stats.HexBonusDealt, 0.001); // 10, unaffected by the crit
            Assert.Equal(45.0 * 0.5, context.Stats.CriticalBonusDealt, 0.001); // 22.5
        }

        [Fact]
        public void DamageTarget_VulnerabilityCrossesAbsorptionToDamage_CreditsTheWholeSwing()
        {
            // The crossover edge: the enemy innately absorbs Fire (FireResistance 1.5 > 1 → the un-hexed hit is a
            // −15 net heal). The player's −0.8 debuff brings live resistance to 0.7, so the hit deals +9. The
            // vulnerability turned a 15 heal into a 9 hit — a 24 swing — credited (÷ (1 + 0.8)) as the enabled
            // marginal. Here the flat-in-base-resistance property intentionally does NOT hold (the innate
            // absorption folds into the marginal); pinned so the crossover behaviour cannot silently drift.
            var player = MakeBattlerWith((Endurance, 0));
            var enemy = MakeBattlerWith((Endurance, 0), (FireResistance, 1.5));
            var context = new BattleContext(player, enemy, timeDelta: 0, new Mulberry32(0));
            context.ApplySkillEffect(Vulnerability(FireResistance, -0.8)); // live FireResistance 0.7, v = 0.8

            context.DamageTarget(30, Single(EDamageType.Fire)); // 30 × (1 − 0.7) = 9 dealt

            Assert.Equal(9, context.Stats.PlayerDamageDealt, 0.001);
            Assert.Equal((9.0 - (-15.0)) / 1.8, context.Stats.HexBonusDealt, 0.001); // 24 / 1.8 = 13.33
        }

        [Fact]
        public void DamageTarget_EnemySelfBuffsResistance_NoPlayerDebuff_BooksNoHexBonus()
        {
            // A hypothetical enemy resistance self-buff (no such content today) raises live resistance ABOVE the
            // innate baseline, so v = innate − live is negative → clamped to 0. The enemy hardening itself never
            // credits the player's Hex; the innate-baseline snapshot handles it for free (no crash, no credit).
            var player = MakeBattlerWith((Endurance, 0));
            var enemy = MakeBattlerWith((Endurance, 0));
            var context = new BattleContext(player, enemy, timeDelta: 0, new Mulberry32(0));
            context.SwapActiveAndTargetBattlers();
            context.ApplySkillEffect(SelfResistanceBuff(FireResistance, 0.5)); // enemy buffs its own Fire resistance
            context.SwapActiveAndTargetBattlers();

            context.DamageTarget(30, Single(EDamageType.Fire)); // 30 × (1 − 0.5) = 15 dealt, no applied vulnerability

            Assert.Equal(15, context.Stats.PlayerDamageDealt, 0.001);
            Assert.Equal(0, context.Stats.HexBonusDealt, 0.001);
        }

        [Fact]
        public void DamageTarget_EnemySelfBuffThenPlayerDebuff_CreditsThePlayersGrossContribution()
        {
            // Both move the same resistance: the enemy self-buffs +0.5 (untracked), the player debuffs −0.8
            // (tracked as v = 0.8). Hex credits the player's GROSS debuff — the resistance its debuff removed —
            // not the net reduction, so the enemy hardening itself does not rob the player of training for the
            // work their debuff did. The marginal is measured against the without-debuff hit (resistance raised
            // back by 0.8 to the enemy-buffed 0.5): N(−0.3) − N(0.5) = 39 − 15 = 24, ÷ (1 + 0.8).
            var player = MakeBattlerWith((Endurance, 0));
            var enemy = MakeBattlerWith((Endurance, 0));
            var context = new BattleContext(player, enemy, timeDelta: 0, new Mulberry32(0));
            context.SwapActiveAndTargetBattlers();
            context.ApplySkillEffect(SelfResistanceBuff(FireResistance, 0.5)); // enemy: FireResistance 0 → 0.5
            context.SwapActiveAndTargetBattlers();
            context.ApplySkillEffect(Vulnerability(FireResistance, -0.8)); // player: 0.5 → −0.3, gross v = 0.8

            context.DamageTarget(30, Single(EDamageType.Fire)); // 30 × (1 − (−0.3)) = 39 dealt

            Assert.Equal(39, context.Stats.PlayerDamageDealt, 0.001);
            Assert.Equal((39.0 - 15.0) / 1.8, context.Stats.HexBonusDealt, 0.001); // 24 / 1.8 ≈ 13.33
        }

        [Fact]
        public void DamageTarget_VulnerabilityExpired_BooksNoHexBonus()
        {
            // The tracked vulnerability rides the effect stack's shared expiry: once the debuff lapses the stack
            // (and its contribution) is gone, so a later hit trains no Hex and lands at full resistance again.
            var player = MakeBattlerWith((Endurance, 0));
            var enemy = MakeBattlerWith((Endurance, 0));
            var context = new BattleContext(player, enemy, timeDelta: 0, new Mulberry32(0));
            context.ApplySkillEffect(Vulnerability(FireResistance, -0.5)); // DurationMs 10_000
            enemy.AdvanceEffects(10_001); // past expiry → the FireResistance stack and its contribution are removed

            context.DamageTarget(30, Single(EDamageType.Fire)); // resistance back to 0 ⇒ 30 dealt, no vulnerability

            Assert.Equal(30, context.Stats.PlayerDamageDealt, 0.001);
            Assert.Equal(0, context.Stats.HexBonusDealt, 0.001);
        }

        [Fact]
        public void DamageTarget_EnemyAttacking_BooksNoHexBonus()
        {
            // Hex is a player-offense signal. Even if the player is somehow vulnerable, an enemy hit never trains
            // the player's Hex — only the player-active branch accrues it.
            var player = MakeBattlerWith((Endurance, 0), (FireResistance, -0.5));
            var enemy = MakeBattlerWith((Endurance, 0));
            var context = new BattleContext(player, enemy, timeDelta: 0, new Mulberry32(0));
            context.SwapActiveAndTargetBattlers();

            context.DamageTarget(30, Single(EDamageType.Fire));

            Assert.Equal(0, context.Stats.HexBonusDealt, 0.001);
        }

        [Fact]
        public void ResolveDamageOverTime_AppliedVulnerability_BooksHexBonusForTheEnemyDot()
        {
            // The player applies a Bleed DoT (100/s) and a Bleed vulnerability (−0.5 BleedResistance) to the
            // enemy, then a 1s tick resolves. The tick deals 100 × (1 − (−0.5)) = 150; the vulnerability enabled
            // 100 × 0.5 = 50 of that, discounted by 1/(1 + 0.5) to the same 33.33 marginal shape as a direct hit.
            var player = MakeBattlerWith((Endurance, 0));
            var enemy = MakeBattlerWith((Endurance, 0));
            var context = new BattleContext(player, enemy, timeDelta: 1000, new Mulberry32(0));
            context.ApplySkillEffect(Vulnerability(BleedResistance, -0.5));
            context.ApplySkillEffect(Dot(BleedDamagePerSecond, 100));

            context.ResolveDamageOverTime();

            Assert.Equal(150, context.Stats.PlayerDamageDealt, 0.001);
            Assert.Equal(100.0 * 0.5 / 1.5, context.Stats.HexBonusDealt, 0.001); // 33.33
        }

        [Fact]
        public void ResolveDamageOverTime_NoVulnerability_BooksNoHexBonus()
        {
            // A plain DoT with no applied vulnerability trains Hex on nothing.
            var player = MakeBattlerWith((Endurance, 0));
            var enemy = MakeBattlerWith((Endurance, 0));
            var context = new BattleContext(player, enemy, timeDelta: 1000, new Mulberry32(0));
            context.ApplySkillEffect(Dot(BleedDamagePerSecond, 100));

            context.ResolveDamageOverTime();

            Assert.Equal(100, context.Stats.PlayerDamageDealt, 0.001);
            Assert.Equal(0, context.Stats.HexBonusDealt, 0.001);
        }

        // ── DamageTarget: Momentum ramp tally (#1428) ─────────────────────────

        [Fact]
        public void DamageTarget_NoRampApplied_BooksNoMomentumBonus()
        {
            // A hit with no self-applied ramp enables no amplification bonus, so Momentum trains on nothing.
            var player = MakeBattlerWith((Endurance, 0));
            var enemy = MakeBattlerWith((Endurance, 0));
            var context = new BattleContext(player, enemy, timeDelta: 0, new Mulberry32(0));

            context.DamageTarget(30, Single(EDamageType.Fire));

            Assert.Equal(0, context.Stats.MomentumBonusDealt, 0.001);
        }

        [Fact]
        public void DamageTarget_InnateAmplification_TrainsNoMomentum()
        {
            // The player already carries +0.5 FireAmplification from a static source (gear), but nothing was
            // applied via a ramp effect — Momentum credits only the ramp's own contribution, so this trains
            // nothing even though the hit itself is amplified.
            var player = MakeBattlerWith((Endurance, 0), (FireAmplification, 0.5));
            var enemy = MakeBattlerWith((Endurance, 0));
            var context = new BattleContext(player, enemy, timeDelta: 0, new Mulberry32(0));

            context.DamageTarget(30, Single(EDamageType.Fire)); // 30 × 1.5 = 45 dealt, but no applied ramp

            Assert.Equal(45, context.Stats.PlayerDamageDealt, 0.001);
            Assert.Equal(0, context.Stats.MomentumBonusDealt, 0.001);
        }

        [Fact]
        public void DamageTarget_AppliedRamp_BooksNormalizedMarginalBonus()
        {
            // The player ramps its own FireAmplification by +0.5, then hits for raw 30. The un-ramped hit would
            // deal 30; the ramped hit deals 45 — the ramp enabled 15, discounted by 1/(1 + 0.5) to 10 (the
            // marginal, saturated on the ramp's own magnitude).
            var player = MakeBattlerWith((Endurance, 0));
            var enemy = MakeBattlerWith((Endurance, 0));
            var context = new BattleContext(player, enemy, timeDelta: 0, new Mulberry32(0));
            context.ApplySkillEffect(Ramp(FireAmplification, 0.5));

            context.DamageTarget(30, Single(EDamageType.Fire)); // 30 × (1 + 0.5) = 45 dealt

            Assert.Equal(45, context.Stats.PlayerDamageDealt, 0.001);
            Assert.Equal(30.0 * 0.5 / 1.5, context.Stats.MomentumBonusDealt, 0.001); // (45 − 30) / (1 + 0.5) = 10
        }

        [Fact]
        public void DamageTarget_HeavilyInvestedRamp_SaturatesTowardOneBaselineHit()
        {
            // A towering ramp (r = 10) enables 10× the un-ramped damage, but the 1/(1 + r) saturation bounds the
            // tally at one baseline (pre-ramp) hit: 30 × 10 / 11 ≈ 27.27 — a monster stack cannot dwarf every
            // other training axis (mirrors the crit-bonus and Hex-bonus ceilings).
            var player = MakeBattlerWith((Endurance, 0));
            var enemy = MakeBattlerWith((Endurance, 0));
            var context = new BattleContext(player, enemy, timeDelta: 0, new Mulberry32(0));
            context.ApplySkillEffect(Ramp(FireAmplification, 10));

            context.DamageTarget(30, Single(EDamageType.Fire));

            Assert.Equal(30.0 * 10.0 / 11.0, context.Stats.MomentumBonusDealt, 0.001);
        }

        [Fact]
        public void DamageTarget_MomentumBonus_IsMitigatedLikeTheRestOfTheHit()
        {
            // Unlike Hex, Momentum amplifies the attacker's own output, so its bonus is mitigated exactly like
            // the rest of the hit (the same property the crit bonus has) — NOT flat in the defender's mitigation.
            // A 50% resistant enemy halves the marginal too: (45 − 30) × (1 − 0.5) / (1 + 0.5) = 5, half the
            // unmitigated 10.
            var player = MakeBattlerWith((Endurance, 0));
            var enemy = MakeBattlerWith((Endurance, 0), (FireResistance, 0.5));
            var context = new BattleContext(player, enemy, timeDelta: 0, new Mulberry32(0));
            context.ApplySkillEffect(Ramp(FireAmplification, 0.5));

            context.DamageTarget(30, Single(EDamageType.Fire)); // 45 × (1 − 0.5) = 22.5 dealt

            Assert.Equal(22.5, context.Stats.PlayerDamageDealt, 0.001);
            Assert.Equal(15.0 * 0.5 / 1.5, context.Stats.MomentumBonusDealt, 0.001); // 5
        }

        [Fact]
        public void DamageTarget_CritAndMomentum_BookIndependentBonuses()
        {
            // Crit and Momentum are separate overlays. Momentum is measured on the pre-crit hit, so a crit does
            // not inflate it: r = 0.5 on raw 30 still banks 10. Crit books its own marginal off the (ramped)
            // non-crit baseline 45: investment m−1 = 1, φ(1) = 0.5 ⇒ 45 × 0.5 = 22.5. Neither overlay balloons
            // the other.
            var player = MakeBattlerWith((CriticalChance, 1), (CriticalDamage, 0.5)); // CriticalDamage 2
            var enemy = MakeBattlerWith((Endurance, 0));
            var context = new BattleContext(player, enemy, timeDelta: 0, new Mulberry32(0));
            context.ApplySkillEffect(Ramp(FireAmplification, 0.5));

            context.DamageTarget(30, Single(EDamageType.Fire)); // ramped non-crit 45, crit ×2 = 90 dealt

            Assert.Equal(30.0 * 0.5 / 1.5, context.Stats.MomentumBonusDealt, 0.001); // 10, unaffected by the crit
            Assert.Equal(45.0 * 0.5, context.Stats.CriticalBonusDealt, 0.001); // 22.5
        }

        [Fact]
        public void DamageTarget_RampExpired_BooksNoMomentumBonus()
        {
            // The tracked ramp rides the caster's own effect stack's shared expiry: once it lapses the stack
            // (and its contribution) is gone, so a later hit trains no Momentum and lands unamplified again.
            var player = MakeBattlerWith((Endurance, 0));
            var enemy = MakeBattlerWith((Endurance, 0));
            var context = new BattleContext(player, enemy, timeDelta: 0, new Mulberry32(0));
            context.ApplySkillEffect(Ramp(FireAmplification, 0.5)); // DurationMs 10_000
            player.AdvanceEffects(10_001); // past expiry → the FireAmplification stack and its contribution are gone

            context.DamageTarget(30, Single(EDamageType.Fire)); // amplification back to 0 ⇒ 30 dealt, no ramp

            Assert.Equal(30, context.Stats.PlayerDamageDealt, 0.001);
            Assert.Equal(0, context.Stats.MomentumBonusDealt, 0.001);
        }

        [Fact]
        public void DamageTarget_EnemyAttacking_BooksNoMomentumBonus()
        {
            // Momentum is a player-offense signal. Even if the enemy has ramped itself, an enemy hit never trains
            // the player's Momentum — only the player-active branch accrues it.
            var player = MakeBattlerWith((Endurance, 0));
            var enemy = MakeBattlerWith((Endurance, 0));
            var context = new BattleContext(player, enemy, timeDelta: 0, new Mulberry32(0));
            context.SwapActiveAndTargetBattlers();
            context.ApplySkillEffect(Ramp(FireAmplification, 0.5)); // enemy ramps its own Fire amplification

            context.DamageTarget(30, Single(EDamageType.Fire));

            Assert.Equal(0, context.Stats.MomentumBonusDealt, 0.001);
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private static IReadOnlyList<SkillDamagePortion> Single(EDamageType type) =>
            [new SkillDamagePortion { Type = type, Weight = 1.0 }];

        private static IReadOnlyList<SkillDamagePortion> Portions(params (EDamageType Type, double Weight)[] portions) =>
            portions.Select(p => new SkillDamagePortion { Type = p.Type, Weight = p.Weight }).ToList();

        private static double TypedDealt(BattleContext context, EDamageType type) =>
            context.Stats.TypedDamageDealt.TryGetValue(type, out var value) ? value : 0;

        private static double Exposure(BattleContext context, EDamageType type) =>
            context.Stats.TypedDamageExposure.TryGetValue(type, out var value) ? value : 0;

        private static BattleContext MakeContext()
        {
            return new BattleContext(MakeBattler(), MakeBattler(), timeDelta: 0, new Mulberry32(0));
        }

        // A player-active context whose enemy carries the given innate Fire resistance, with a −0.5 FireResistance
        // vulnerability already applied by the player (so v = 0.5 regardless of the base resistance).
        private static BattleContext MakeHexContext(double innateFireResistance)
        {
            var player = MakeBattlerWith((EAttribute.Endurance, 0));
            var enemy = MakeBattlerWith((EAttribute.Endurance, 0), (EAttribute.FireResistance, innateFireResistance));
            var context = new BattleContext(player, enemy, timeDelta: 0, new Mulberry32(0));
            context.ApplySkillEffect(Vulnerability(EAttribute.FireResistance, -0.5));
            return context;
        }

        // A player-cast Opponent-targeted resistance debuff (negative additive) — the vulnerability enabler Hex
        // trains on. Applied via BattleContext.ApplySkillEffect so it lands on the target (enemy) battler.
        private static SkillEffect Vulnerability(EAttribute resistance, double amount) => new()
        {
            Id = 1,
            Target = ESkillEffectTarget.Opponent,
            AttributeId = resistance,
            ModifierType = EModifierType.Additive,
            Amount = amount,
            DurationMs = 10_000,
            ScalingAttributeId = EAttribute.Strength,
            ScalingAmount = 0,
        };

        // A self-targeted resistance buff (positive additive) — an enemy hardening its own resistance mid-battle.
        // Cast while the enemy is the active battler so it lands on the enemy (the Hex isolation edge case).
        private static SkillEffect SelfResistanceBuff(EAttribute resistance, double amount) => new()
        {
            Id = 3,
            Target = ESkillEffectTarget.Self,
            AttributeId = resistance,
            ModifierType = EModifierType.Additive,
            Amount = amount,
            DurationMs = 10_000,
            ScalingAttributeId = EAttribute.Strength,
            ScalingAmount = 0,
        };

        // A player-cast Self-targeted amplification buff (positive additive) — the ramp enabler Momentum trains
        // on (#1428). Applied via BattleContext.ApplySkillEffect so it lands on the active (casting) battler.
        private static SkillEffect Ramp(EAttribute amplification, double amount) => new()
        {
            Id = 4,
            Target = ESkillEffectTarget.Self,
            AttributeId = amplification,
            ModifierType = EModifierType.Additive,
            Amount = amount,
            DurationMs = 10_000,
            ScalingAttributeId = EAttribute.Strength,
            ScalingAmount = 0,
        };

        // A player-cast Opponent-targeted DoT accumulator effect (per-second amount) for the DoT Hex tests.
        private static SkillEffect Dot(EAttribute accumulator, double perSecond) => new()
        {
            Id = 2,
            Target = ESkillEffectTarget.Opponent,
            AttributeId = accumulator,
            ModifierType = EModifierType.Additive,
            Amount = perSecond,
            DurationMs = 10_000,
            ScalingAttributeId = EAttribute.Strength,
            ScalingAmount = 0,
        };

        private static Battler MakeBattler()
        {
            return MakeBattlerWith((EAttribute.Endurance, 50));
        }

        private static Battler MakeBattlerWith(params (EAttribute Attribute, double Amount)[] attributes)
        {
            var statAllocations = attributes
                .Select(a => new StatAllocation { Attribute = a.Attribute, Amount = a.Amount })
                .ToList();
            var player = new PlayerBuilder().WithStatAllocations(statAllocations).Build();
            return BattlerFactory.FromPlayer(player);
        }
    }
}
