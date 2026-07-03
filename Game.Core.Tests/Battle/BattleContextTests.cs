using Game.Core;
using Game.Core.Battle;
using Game.Core.Items;
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
            // baseCriticalChance 1 always succeeds; CriticalDamage is the base 1.5 + 0.5 = 2, read directly as the multiplier.
            var player = MakeBattlerWith((CriticalDamage, 0.5));
            var enemy = MakeBattlerWith((Endurance, 0)); // MaxHealth 50, Toughness 0
            var context = new BattleContext(player, enemy, timeDelta: 0, new Mulberry32(0));

            context.DamageTarget(20, Single(EDamageType.Physical), 1); // crit ⇒ 20×2 = 40; no Toughness ⇒ 40 dealt

            Assert.Equal(50 - 40, enemy.CurrentHealth, 0.001);
            Assert.Equal(40, context.Stats.PlayerDamageDealt, 0.001);
        }

        [Fact]
        public void DamageTarget_PlayerNoCrit_DealsRawUnmitigated()
        {
            var player = MakeBattlerWith((CriticalDamage, 2));
            var enemy = MakeBattlerWith((Endurance, 0)); // Toughness 0
            var context = new BattleContext(player, enemy, timeDelta: 0, new Mulberry32(0));

            context.DamageTarget(20, Single(EDamageType.Physical), 0); // no crit ⇒ 20, no Toughness ⇒ 20

            Assert.Equal(50 - 20, enemy.CurrentHealth, 0.001);
        }

        // ── DamageTarget: per-skill crit enabler × CriticalChanceMultiplier (#1453) ──

        [Fact]
        public void DamageTarget_ZeroBaseCriticalChance_NeverCritsEvenWithHeavyMultiplierInvestment()
        {
            // The enabler is the SKILL's own base chance, not the multiplier: a build stacking
            // CriticalChanceMultiplier (base 1 + 999 = 1000) still crits for nothing when the fired skill's own
            // CriticalChance is 0 (0 × 1000 = 0) — crit stays a committed per-skill identity, not a free stat
            // any investment in the multiplier alone can unlock.
            var player = MakeBattlerWith((CriticalChanceMultiplier, 999));
            var enemy = MakeBattlerWith((Endurance, 0));
            var context = new BattleContext(player, enemy, timeDelta: 0, new Mulberry32(0));

            context.DamageTarget(20, Single(EDamageType.Physical), 0); // baseCriticalChance 0 ⇒ never crits

            Assert.Equal(50 - 20, enemy.CurrentHealth, 0.001);
            Assert.Equal(0, context.Stats.CriticalHits);
        }

        [Fact]
        public void DamageTarget_MultiplierComposesMultiplicativelyWithTheSkillsBase()
        {
            // CriticalChanceMultiplier zeroed out (base 1 + (-1) = 0) cancels even a skill authored to always
            // crit on its own (baseCriticalChance 1): 1 × 0 = 0 — proving the composition is a product, not an
            // independent OR, so a multiplier debuff can fully negate a committed skill's own investment.
            var player = MakeBattlerWith((CriticalChanceMultiplier, -1));
            var enemy = MakeBattlerWith((Endurance, 0));
            var context = new BattleContext(player, enemy, timeDelta: 0, new Mulberry32(0));

            context.DamageTarget(20, Single(EDamageType.Physical), 1); // 1 × 0 ⇒ never crits

            Assert.Equal(50 - 20, enemy.CurrentHealth, 0.001);
            Assert.Equal(0, context.Stats.CriticalHits);
        }

        [Fact]
        public void DamageTarget_MultiplierScalesAFractionalBaseAboveOne_AlwaysCrits()
        {
            // A fractional skill base (0.5) scaled by a CriticalChanceMultiplier of 2 (base 1 + 1 = 2) reaches an
            // effective chance of 1.0 — at or above every possible [0,1) RNG draw, so the roll always succeeds
            // even though neither factor alone would guarantee it. CriticalDamage is the base 1.5 + 0.5 = 2.
            var player = MakeBattlerWith((CriticalChanceMultiplier, 1), (CriticalDamage, 0.5));
            var enemy = MakeBattlerWith((Endurance, 0));
            var context = new BattleContext(player, enemy, timeDelta: 0, new Mulberry32(0));

            context.DamageTarget(20, Single(EDamageType.Physical), 0.5); // 0.5 × 2 = 1.0 ⇒ always crits

            Assert.Equal(1, context.Stats.CriticalHits);
            Assert.Equal(50 - 40, enemy.CurrentHealth, 0.001); // 20 × 2 (CriticalDamage), no Toughness
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

            context.DamageTarget(20, Single(EDamageType.Physical), 0); // dodged ⇒ 0

            Assert.Equal(before, player.CurrentHealth, 0.001);
            Assert.Equal(0, context.Stats.PlayerDamageTaken, 0.001);
        }

        [Fact]
        public void DamageTarget_EnemyAttacking_NeverCrits()
        {
            // A forced baseCriticalChance of 1 is passed, but the crit roll is gated on the player attacking, so
            // the enemy's hit lands un-multiplied (20, not 40) even though its CriticalDamage would double it.
            // The player has no Toughness, so it lands in full.
            var player = MakeBattlerWith((Endurance, 0));
            var enemy = MakeBattlerWith((CriticalDamage, 2));
            var context = new BattleContext(player, enemy, timeDelta: 0, new Mulberry32(0));
            context.SwapActiveAndTargetBattlers();
            var before = player.CurrentHealth;

            context.DamageTarget(20, Single(EDamageType.Physical), 1);

            Assert.Equal(before - 20, player.CurrentHealth, 0.001);
        }

        // ── DamageTarget: crit/dodge/block statistics ────────────────────────

        [Fact]
        public void DamageTarget_PlayerCrit_RecordsCritHitAndPostMitigationDamage()
        {
            var player = MakeBattlerWith((CriticalDamage, 0.5)); // CriticalDamage 2
            var enemy = MakeBattlerWith((Endurance, 0)); // Toughness 0
            var context = new BattleContext(player, enemy, timeDelta: 0, new Mulberry32(0));

            context.DamageTarget(20, Single(EDamageType.Physical), 1); // 20×2 = 40, no Toughness ⇒ 40 dealt

            Assert.Equal(1, context.Stats.CriticalHits);
            // The player-facing statistic is the actual full crit damage dealt.
            Assert.Equal(40, context.Stats.CriticalDamageDealt, 0.001);
            // The Precision signal is the share claim (#1481): the crit hit's booked (landed) 40 × φ(m−1) with
            // investment m−1 = 1, φ(1) = 0.5 ⇒ 20.
            Assert.Equal(20, context.Stats.CriticalBonusDealt, 0.001);
        }

        [Fact]
        public void DamageTarget_PlayerNoCrit_RecordsNoCritStatistics()
        {
            var player = MakeBattlerWith((CriticalDamage, 2));
            var enemy = MakeBattlerWith((Endurance, 0));
            var context = new BattleContext(player, enemy, timeDelta: 0, new Mulberry32(0));

            context.DamageTarget(20, Single(EDamageType.Physical), 0);

            Assert.Equal(0, context.Stats.CriticalHits);
            Assert.Equal(0, context.Stats.CriticalDamageDealt, 0.001);
            // A build that never crits trains Precision on nothing.
            Assert.Equal(0, context.Stats.CriticalBonusDealt, 0.001);
        }

        [Fact]
        public void DamageTarget_MinimallyInvestedCrit_BooksASmallShare()
        {
            // Base CriticalDamage only (1.5, no invested bonus): m = 1.5, investment m−1 = 0.5, φ(0.5) = 1/3.
            // The booked crit hit is 30 ⇒ claim 10 — a token crit claims a far smaller share of its own hit
            // than a committed one (strength-proportionality lives in φ, #1481).
            var player = MakeBattlerWith(); // CriticalDamage 1.5 (base)
            var enemy = MakeBattlerWith((Endurance, 0));
            var context = new BattleContext(player, enemy, timeDelta: 0, new Mulberry32(0));

            context.DamageTarget(20, Single(EDamageType.Physical), 1); // 20 × 1.5 = 30 dealt

            Assert.Equal(30, context.Stats.CriticalDamageDealt, 0.001);
            Assert.Equal(30.0 / 3.0, context.Stats.CriticalBonusDealt, 0.001);
        }

        [Fact]
        public void DamageTarget_HeavilyInvestedCrit_ClaimsAtMostTheHealthRemoved()
        {
            // Heavy crit-damage investment: CriticalDamage 1.5 + 98.5 = 100, investment 99, φ(99) = 0.99. The
            // 2000-damage crit overkills the 50-HP enemy, so the booked basis is the 50 actually removed (#1482)
            // and the claim is 50 × 0.99 = 49.5 — a monster crit cannot mint training beyond the health it
            // removed, and φ bounds the share below even that.
            var player = MakeBattlerWith((CriticalDamage, 98.5));
            var enemy = MakeBattlerWith((Endurance, 0));
            var context = new BattleContext(player, enemy, timeDelta: 0, new Mulberry32(0));

            context.DamageTarget(20, Single(EDamageType.Physical), 1); // 20 × 100 = 2000 dealt

            Assert.Equal(2000, context.Stats.CriticalDamageDealt, 0.001);
            Assert.Equal(49.5, context.Stats.CriticalBonusDealt, 0.001);
        }

        [Fact]
        public void DamageTarget_PlayerDodge_RecordsDodgeAndPostMitigationDamageAvoided()
        {
            var player = MakeBattlerWith((DodgeChance, 1)); // Toughness 0
            var enemy = MakeBattlerWith((Endurance, 0));
            var context = new BattleContext(player, enemy, timeDelta: 0, new Mulberry32(0));
            context.SwapActiveAndTargetBattlers();

            context.DamageTarget(20, Single(EDamageType.Physical), 0); // would deal 20 (no Toughness), fully avoided

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

            context.DamageTarget(20, Single(EDamageType.Physical), 0);

            Assert.Equal(0, context.Stats.AttacksDodged);
            Assert.Equal(0, context.Stats.DamageDodged, 0.001);
        }

        // ── DamageTarget: RNG draw order ─────────────────────────────────────

        [Fact]
        public void DamageTarget_DrawsOncePerPlayerHit_TwicePerEnemyHit()
        {
            // The draw count is a pure function of the fire sequence: one crit draw when the player attacks,
            // then a parry draw followed by a dodge draw when the enemy attacks — both unconditional (#1457;
            // Block's second draw was retired earlier, spike #1330). Verified by comparing the shared stream's
            // position against a reference advanced by exactly three draws — independent of the seed. With
            // ParryChance at its default 0 the parry never procs, so no third (counter-crit) draw is taken.
            const uint seed = 12345u;
            var rng = new Mulberry32(seed);
            var player = MakeBattlerWith((Endurance, 0));
            var enemy = MakeBattlerWith((Endurance, 0));
            var context = new BattleContext(player, enemy, timeDelta: 0, rng);

            context.DamageTarget(5, Single(EDamageType.Physical), 0);               // player attacking → 1 draw
            context.SwapActiveAndTargetBattlers();
            context.DamageTarget(5, Single(EDamageType.Physical), 0);               // enemy attacking → 2 draws

            var reference = new Mulberry32(seed);
            reference.Next();
            reference.Next();
            reference.Next();
            Assert.Equal(reference.Next(), rng.Next());
        }

        // ── DamageTarget: parry / riposte (#1457) ────────────────────────────

        [Fact]
        public void DamageTarget_PlayerParries_NegatesHitAndRecordsParryStatistics()
        {
            // No counter skill resolved (MakeBattlerWith carries no items), so the parry negates the hit but
            // fires no riposte.
            var player = MakeBattlerWith((ParryChance, 1));
            var enemy = MakeBattlerWith((Endurance, 0));
            var context = new BattleContext(player, enemy, timeDelta: 0, new Mulberry32(0));
            context.SwapActiveAndTargetBattlers(); // enemy attacks the player
            var before = player.CurrentHealth;

            context.DamageTarget(20, Single(EDamageType.Physical), 0); // parried ⇒ 0, no Toughness on either side

            Assert.Equal(before, player.CurrentHealth, 0.001);
            Assert.Equal(0, context.Stats.PlayerDamageTaken, 0.001);
            Assert.Equal(1, context.Stats.AttacksParried);
            Assert.Equal(20, context.Stats.DamageParried, 0.001);
            Assert.Equal(0, context.Stats.AttacksDodged); // parry is checked first — dodge never triggers
            Assert.Equal(0, context.Stats.PlayerCounterDamageDealt, 0.001);
        }

        [Fact]
        public void DamageTarget_ParryChecksBeforeDodge_BothCommitted_StillParries()
        {
            // Both ParryChance and DodgeChance forced to 1: parry wins since it is checked first (the two
            // avoidance layers don't compete for the same draw — a dodge investment never starves riposte).
            var player = MakeBattlerWith((ParryChance, 1), (DodgeChance, 1));
            var enemy = MakeBattlerWith((Endurance, 0));
            var context = new BattleContext(player, enemy, timeDelta: 0, new Mulberry32(0));
            context.SwapActiveAndTargetBattlers();

            context.DamageTarget(20, Single(EDamageType.Physical), 0);

            Assert.Equal(1, context.Stats.AttacksParried);
            Assert.Equal(0, context.Stats.AttacksDodged);
        }

        [Fact]
        public void DamageTarget_PlayerParries_FiresCounterThroughWeaponSignature()
        {
            var counterSkill = MakeCounterSkill(baseDamage: 10);
            var player = MakeBattlerWithCounter(counterSkill, (ParryChance, 1));
            var enemy = MakeBattlerWith((Endurance, 0)); // Toughness 0, no resistance
            var context = new BattleContext(player, enemy, timeDelta: 0, new Mulberry32(0));
            context.SwapActiveAndTargetBattlers(); // enemy attacks the player
            var enemyBefore = enemy.CurrentHealth;

            context.DamageTarget(20, Single(EDamageType.Physical), 0); // the original hit is parried

            // The counter is the resolved weapon signature's own raw damage (10), routed through the same
            // player-fire path — no cooldown, no effects, no per-skill attribution.
            Assert.Equal(enemyBefore - 10, enemy.CurrentHealth, 0.001);
            Assert.Equal(10, context.Stats.PlayerCounterDamageDealt, 0.001);
            Assert.Equal(10, context.Stats.PlayerDamageDealt, 0.001); // only the counter — the original hit was 0
            Assert.Empty(context.Stats.SkillStats); // RecordSkillUse is never called for the counter
        }

        [Fact]
        public void DamageTarget_ParryCounter_CanCritOnItsOwnAuthoredChance()
        {
            var counterSkill = MakeCounterSkill(baseDamage: 10, criticalChance: 1);
            var player = MakeBattlerWithCounter(counterSkill, (ParryChance, 1), (CriticalDamage, 0.5)); // CriticalDamage 2
            var enemy = MakeBattlerWith((Endurance, 0));
            var context = new BattleContext(player, enemy, timeDelta: 0, new Mulberry32(0));
            context.SwapActiveAndTargetBattlers();
            var enemyBefore = enemy.CurrentHealth;

            context.DamageTarget(20, Single(EDamageType.Physical), 0);

            Assert.Equal(enemyBefore - 20, enemy.CurrentHealth, 0.001); // 10 × 2 (crit)
            Assert.Equal(1, context.Stats.CriticalHits);
            Assert.Equal(20, context.Stats.PlayerCounterDamageDealt, 0.001);
        }

        [Fact]
        public void DamageTarget_ParryCounter_EnemyReflectionAppliesToTheCounter()
        {
            // The counter is routed through the shared player-fire path, so an enemy's own DamageReflection
            // reflects the counter back onto the player — "falls out of one code path" rather than a bespoke branch.
            var counterSkill = MakeCounterSkill(baseDamage: 10);
            var player = MakeBattlerWithCounter(counterSkill, (ParryChance, 1), (Endurance, 50)); // Toughness 100
            var enemy = MakeBattlerWith((Endurance, 0), (DamageReflection, 0.5));
            var context = new BattleContext(player, enemy, timeDelta: 0, new Mulberry32(0));
            context.SwapActiveAndTargetBattlers();
            var playerBefore = player.CurrentHealth;

            context.DamageTarget(20, Single(EDamageType.Physical), 0);

            Assert.Equal(10, context.Stats.PlayerCounterDamageDealt, 0.001);
            Assert.Equal(playerBefore - 5, player.CurrentHealth, 0.001); // 10 × 0.5 reflected back, unmitigated
        }

        [Fact]
        public void DamageTarget_ParryProc_NoCounterSkill_AdvancesStreamByTwoDraws()
        {
            // Parry then dodge, both unconditional, is the whole draw cost when there is no counter skill to
            // fire (no third crit draw) — independent of the seed.
            const uint seed = 777u;
            var rng = new Mulberry32(seed);
            var player = MakeBattlerWith((ParryChance, 1));
            var enemy = MakeBattlerWith((Endurance, 0));
            var context = new BattleContext(player, enemy, timeDelta: 0, rng);
            context.SwapActiveAndTargetBattlers();

            context.DamageTarget(20, Single(EDamageType.Physical), 0);

            var reference = new Mulberry32(seed);
            reference.Next(); // parry draw
            reference.Next(); // dodge draw (unconditional)
            Assert.Equal(reference.Next(), rng.Next());
        }

        [Fact]
        public void DamageTarget_ParryProc_WithCounterSkill_AdvancesStreamByThreeDraws()
        {
            // A proc'd parry consumes exactly one more draw than the no-counter case: the counter fire's own
            // crit draw, taken even at 0 authored chance (mirroring the ordinary per-fire crit draw).
            const uint seed = 777u;
            var rng = new Mulberry32(seed);
            var counterSkill = MakeCounterSkill(baseDamage: 10);
            var player = MakeBattlerWithCounter(counterSkill, (ParryChance, 1));
            var enemy = MakeBattlerWith((Endurance, 0));
            var context = new BattleContext(player, enemy, timeDelta: 0, rng);
            context.SwapActiveAndTargetBattlers();

            context.DamageTarget(20, Single(EDamageType.Physical), 0);

            var reference = new Mulberry32(seed);
            reference.Next(); // parry draw
            reference.Next(); // dodge draw (unconditional)
            reference.Next(); // the counter fire's crit draw
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

            var dealt = context.DamageTarget(40, Single(EDamageType.Physical), 0); // 40 to the enemy

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

            context.DamageTarget(50, Single(EDamageType.Physical), 0);

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

            context.DamageTarget(50, Single(EDamageType.Physical), 0);

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
            context.DamageTarget(30, Single(EDamageType.Physical), 0); // enemy 50 → 20; the enemy reflects this 30 onto the player
            var playerBefore = player.CurrentHealth;

            context.DamageTarget(20, Single(EDamageType.Fire), 0); // 20 × (1 − 2) = −20, absorbed (enemy 20 → 40) — no reflection

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

            context.DamageTarget(40, Single(EDamageType.Fire), 0);

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

            context.DamageTarget(40, Single(EDamageType.Fire), 0);

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
            context.DamageTarget(30, Single(EDamageType.Physical), 0); // 30 (no Toughness) → CurrentHealth 20

            context.DamageTarget(20, Single(EDamageType.Fire), 0);

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
            var player = MakeBattlerWith((CriticalDamage, 0.5)); // CriticalDamage 2.0
            var enemy = MakeBattlerWith((Endurance, 0), (Toughness, 20), (FireResistance, 0.5)); // Toughness 20, MaxHealth 50
            var context = new BattleContext(player, enemy, timeDelta: 0, new Mulberry32(0));

            context.DamageTarget(20, Single(EDamageType.Fire), 1);

            Assert.Equal(50 - 10, enemy.CurrentHealth, 0.001);
        }

        // ── DamageTarget: typed damage dealt (offense book, #1337) ───────────

        [Fact]
        public void DamageTarget_PlayerHit_RecordsTypedDamageDealtMatchingPlayerDamageDealt()
        {
            var player = MakeBattlerWith((Endurance, 0));
            var enemy = MakeBattlerWith((Endurance, 0)); // Toughness 0
            var context = new BattleContext(player, enemy, timeDelta: 0, new Mulberry32(0));

            context.DamageTarget(20, Single(EDamageType.Fire), 0); // no Toughness ⇒ 20 dealt

            // The typed offense book sums the same post-mitigation figure each hit booked into
            // PlayerDamageDealt (no overkill here, so the #1482 cap doesn't bite).
            Assert.Equal(20, context.Stats.PlayerDamageDealt, 0.001);
            Assert.Equal(20, TypedDealt(context, EDamageType.Fire), 0.001);
        }

        [Fact]
        public void DamageTarget_KillingHit_BooksOnlyTheHealthRemoved()
        {
            // The 80-damage hit overkills the 50-HP enemy by 30: the typed offense book is capped at the
            // health actually removed (#1482), while the health math and the whole-hit stats keep the full
            // net — overkill is real "biggest hit" feedback, it just isn't training activity.
            var player = MakeBattlerWith((Endurance, 0));
            var enemy = MakeBattlerWith((Endurance, 0)); // Toughness 0, MaxHealth 50
            var context = new BattleContext(player, enemy, timeDelta: 0, new Mulberry32(0));

            context.DamageTarget(80, Single(EDamageType.Fire), 0);

            Assert.Equal(-30, enemy.CurrentHealth, 0.001); // the health math itself is uncapped
            Assert.Equal(50, TypedDealt(context, EDamageType.Fire), 0.001);
            Assert.Equal(80, context.Stats.PlayerDamageDealt, 0.001);
            Assert.Equal(80, context.Stats.HighestPlayerAttack, 0.001);
        }

        [Fact]
        public void DamageTarget_MultiPortionKillingSwing_CapsInPortionOrder()
        {
            // A 200-raw swing split evenly: the first (Physical) portion's 100 exhausts the 50 HP, so the
            // second (Fire) portion books 0 — the #1482 cap distributes across portions in the fixed order.
            var player = MakeBattlerWith((Endurance, 0));
            var enemy = MakeBattlerWith((Endurance, 0));
            var context = new BattleContext(player, enemy, timeDelta: 0, new Mulberry32(0));

            context.DamageTarget(200, Portions((EDamageType.Physical, 1.0), (EDamageType.Fire, 1.0)), 0);

            Assert.Equal(50, TypedDealt(context, EDamageType.Physical), 0.001);
            Assert.Equal(0, TypedDealt(context, EDamageType.Fire), 0.001);
            Assert.Equal(200, context.Stats.PlayerDamageDealt, 0.001); // the whole-hit total keeps the full net
        }

        [Fact]
        public void DamageTarget_AbsorbedHit_BooksTheNegativeNetUnchanged()
        {
            // The #1482 cap trims only positive overkill: an absorbed hit's negative net (the capped heal
            // TakeDamage reports) still books through to the typed book exactly as before.
            var player = MakeBattlerWith((Endurance, 0));
            var enemy = MakeBattlerWith((Endurance, 0), (FireResistance, 2.0)); // Toughness 0, MaxHealth 50
            var context = new BattleContext(player, enemy, timeDelta: 0, new Mulberry32(0));
            context.DamageTarget(30, Single(EDamageType.Physical), 0); // 50 → 20, room for the heal

            context.DamageTarget(20, Single(EDamageType.Fire), 0); // 20 × (1 − 2) = −20, absorbed

            Assert.Equal(30, TypedDealt(context, EDamageType.Physical), 0.001);
            Assert.Equal(-20, TypedDealt(context, EDamageType.Fire), 0.001);
        }

        [Fact]
        public void DamageTarget_PlayerHitsDifferentTypes_AccumulatedPerType()
        {
            var player = MakeBattlerWith((Endurance, 0));
            // MaxHealth 100 so the 60 total doesn't kill — the overkill booking cap (#1482) has its own tests.
            var enemy = MakeBattlerWith((Endurance, 0), (MaxHealth, 50));
            var context = new BattleContext(player, enemy, timeDelta: 0, new Mulberry32(0));

            context.DamageTarget(20, Single(EDamageType.Fire), 0);     // 20
            context.DamageTarget(10, Single(EDamageType.Fire), 0);     // 10
            context.DamageTarget(30, Single(EDamageType.Physical), 0); // 30

            Assert.Equal(30, TypedDealt(context, EDamageType.Fire), 0.001); // 20 + 10
            Assert.Equal(30, TypedDealt(context, EDamageType.Physical), 0.001);
        }

        [Fact]
        public void DamageTarget_PlayerCrit_TypedDamageDealtUsesPostMitigationActual()
        {
            var player = MakeBattlerWith((CriticalDamage, 0.5)); // CriticalDamage 2
            var enemy = MakeBattlerWith((Endurance, 0)); // Toughness 0
            var context = new BattleContext(player, enemy, timeDelta: 0, new Mulberry32(0));

            context.DamageTarget(20, Single(EDamageType.Physical), 1); // 20×2 = 40, no Toughness ⇒ 40

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

            context.DamageTarget(20, Single(EDamageType.Physical), 0);

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

            context.DamageTarget(40, Single(EDamageType.Fire), 0);

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

            context.DamageTarget(40, Single(EDamageType.Fire), 0);

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

            context.DamageTarget(20, Single(EDamageType.Physical), 0);

            Assert.Empty(context.Stats.TypedDamageExposure);
        }

        // ── DamageTarget: resistance-only mitigated slice of exposure (resist-training split, #1454) ─

        [Fact]
        public void DamageTarget_EnemyHit_RecordsResistanceMitigatedAmount()
        {
            // The resistance-only mitigated slice of the exposure: 40 Fire × 0.5 FireResistance = 20, matching
            // the reduction FireResistance alone accounts for (the player has no Toughness, so it is the whole
            // reduction — PlayerDamageTaken is 20 too).
            var player = MakeBattlerWith((Endurance, 0), (FireResistance, 0.5)); // Toughness 0
            var enemy = MakeBattlerWith((Endurance, 0));
            var context = new BattleContext(player, enemy, timeDelta: 0, new Mulberry32(0));
            context.SwapActiveAndTargetBattlers();

            context.DamageTarget(40, Single(EDamageType.Fire), 0);

            Assert.Equal(20, context.Stats.PlayerDamageTaken, 0.001);
            Assert.Equal(20, Mitigated(context, EDamageType.Fire), 0.001);
        }

        [Fact]
        public void DamageTarget_EnemyHit_ToughnessAloneRecordsNoResistanceMitigation()
        {
            // Toughness is deliberately excluded from the resist-training split (#1454): a Toughness-only build
            // blocks real damage (PlayerDamageTaken well under the pre-mitigation hit) but banks zero
            // resistance-mitigated credit, since Toughness is a generic stat every build can raise, not the
            // type-specific resistance investment a resist path represents.
            var player = MakeBattlerWith((Endurance, 50)); // Toughness 100 derived, no FireResistance
            var enemy = MakeBattlerWith((Endurance, 0));
            var context = new BattleContext(player, enemy, timeDelta: 0, new Mulberry32(0));
            context.SwapActiveAndTargetBattlers();

            context.DamageTarget(40, Single(EDamageType.Fire), 0);

            Assert.True(context.Stats.PlayerDamageTaken < 40);
            Assert.Equal(0, Mitigated(context, EDamageType.Fire), 0.001);
        }

        [Fact]
        public void DamageTarget_EnemyHit_VulnerableResistanceRecordsNoResistanceMitigation()
        {
            // A negative summed resistance (a vulnerability debuff) is anti-mitigation, not resistance — it
            // blocks nothing, so the mitigated slice clamps to 0 rather than going negative.
            var player = MakeBattlerWith((Endurance, 0), (FireResistance, -0.5)); // Toughness 0
            var enemy = MakeBattlerWith((Endurance, 0));
            var context = new BattleContext(player, enemy, timeDelta: 0, new Mulberry32(0));
            context.SwapActiveAndTargetBattlers();

            context.DamageTarget(40, Single(EDamageType.Fire), 0);

            Assert.Equal(0, Mitigated(context, EDamageType.Fire), 0.001);
        }

        [Fact]
        public void DamageTarget_EnemyHit_AbsorptionResistanceCreditsAtMostTheFullDealtAmount()
        {
            // A summed resistance above 1 drives the live hit into absorption (a net heal, capped at 0 here since
            // the player starts at full health with no room to heal into), but the resist-training credit is a
            // weighting fraction, not a damage multiplier — it clamps at 1, so it credits at most the full
            // pre-mitigation dealt amount rather than crediting more than was dealt.
            var player = MakeBattlerWith((Endurance, 0), (FireResistance, 2.0)); // Toughness 0
            var enemy = MakeBattlerWith((Endurance, 0));
            var context = new BattleContext(player, enemy, timeDelta: 0, new Mulberry32(0));
            context.SwapActiveAndTargetBattlers();

            context.DamageTarget(40, Single(EDamageType.Fire), 0);

            Assert.Equal(40, Mitigated(context, EDamageType.Fire), 0.001);
        }

        [Fact]
        public void DamageTarget_PlayerDodge_RecordsNoResistanceMitigation()
        {
            // A dodged hit was evaded, not mitigated — like exposure, it does not feed the resist-training split.
            var player = MakeBattlerWith((DodgeChance, 1), (FireResistance, 0.5));
            var enemy = MakeBattlerWith((Endurance, 0));
            var context = new BattleContext(player, enemy, timeDelta: 0, new Mulberry32(0));
            context.SwapActiveAndTargetBattlers();

            context.DamageTarget(40, Single(EDamageType.Fire), 0);

            Assert.Empty(context.Stats.TypedDamageResistanceMitigated);
        }

        [Fact]
        public void DamageTarget_PlayerActive_RecordsNoExposure()
        {
            // The incoming book is the player's exposure; the player's own hits never add to it.
            var player = MakeBattlerWith((Endurance, 0));
            var enemy = MakeBattlerWith((Endurance, 0));
            var context = new BattleContext(player, enemy, timeDelta: 0, new Mulberry32(0));

            context.DamageTarget(20, Single(EDamageType.Physical), 0);

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
            // 0.5 (Physical unresisted), no Toughness: 60 + 40×0.5 = 60 + 20 = 80 net. MaxHealth 100 so the
            // swing doesn't kill — the overkill booking cap (#1482) has its own tests.
            var player = MakeBattlerWith((Endurance, 0));
            var enemy = MakeBattlerWith((Endurance, 0), (FireResistance, 0.5), (MaxHealth, 50)); // MaxHealth 100, Toughness 0
            var context = new BattleContext(player, enemy, timeDelta: 0, new Mulberry32(0));

            var dealt = context.DamageTarget(100, Portions((EDamageType.Physical, 60), (EDamageType.Fire, 40)), 0);

            Assert.Equal(80, dealt, 0.001);
            Assert.Equal(80, context.Stats.PlayerDamageDealt, 0.001);
            Assert.Equal(100 - 80, enemy.CurrentHealth, 0.001);
            // Per-portion typed offense book: each portion's own post-mitigation net under its type.
            Assert.Equal(60, TypedDealt(context, EDamageType.Physical), 0.001);
            Assert.Equal(20, TypedDealt(context, EDamageType.Fire), 0.001);
        }

        [Fact]
        public void DamageTarget_MultiPortion_SingleCritMultipliesEveryPortion()
        {
            // baseCriticalChance 1, CriticalDamage 1.5 + 0.5 = 2. One crit draw scales BOTH portions: [Physical 50,
            // Fire 50] of raw 20 → 10 each, ×2 → 20 each = 40 net (a per-portion-only crit would give 30).
            var player = MakeBattlerWith((CriticalDamage, 0.5));
            var enemy = MakeBattlerWith((Endurance, 0)); // MaxHealth 50, Toughness 0
            var context = new BattleContext(player, enemy, timeDelta: 0, new Mulberry32(0));

            var dealt = context.DamageTarget(20, Portions((EDamageType.Physical, 50), (EDamageType.Fire, 50)), 1);

            Assert.Equal(40, dealt, 0.001);
            Assert.Equal(50 - 40, enemy.CurrentHealth, 0.001);
            // A crit is one hit; the crit damage stat uses the whole-hit summed net.
            Assert.Equal(1, context.Stats.CriticalHits);
            Assert.Equal(40, context.Stats.CriticalDamageDealt, 0.001);
            // The share claim sums each portion's booked (landed) damage (20 + 20 = 40) before φ: investment
            // m−1 = 1, φ(1) = 0.5 ⇒ 20, so a multi-typed crit trains Precision as one whole-hit claim (#1481).
            Assert.Equal(20, context.Stats.CriticalBonusDealt, 0.001);
        }

        [Fact]
        public void DamageTarget_MultiPortion_OneDrawPerFireRegardlessOfPortionCount()
        {
            // The per-fire draw count is independent of portion count: a 3-portion player fire then a 3-portion
            // enemy fire advance the shared stream by exactly three (one crit draw, then a parry + dodge draw, #1457).
            const uint seed = 4242u;
            var rng = new Mulberry32(seed);
            var player = MakeBattlerWith((Endurance, 0));
            var enemy = MakeBattlerWith((Endurance, 0));
            var context = new BattleContext(player, enemy, timeDelta: 0, rng);
            var threePortions = Portions((EDamageType.Physical, 1), (EDamageType.Fire, 1), (EDamageType.Water, 1));

            context.DamageTarget(9, threePortions, 0);              // player attacking → 1 draw
            context.SwapActiveAndTargetBattlers();
            context.DamageTarget(9, threePortions, 0);              // enemy attacking → 2 draws

            var reference = new Mulberry32(seed);
            reference.Next();
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

            var dealt = context.DamageTarget(40, Portions((EDamageType.Physical, 50), (EDamageType.Fire, 50)), 0);

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

            var dealt = context.DamageTarget(40, Portions((EDamageType.Physical, 50), (EDamageType.Fire, 50)), 0);

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

            var dealt = context.DamageTarget(40, Portions((EDamageType.Physical, 50), (EDamageType.Fire, 50)), 0);

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

            var dealt = context.DamageTarget(100, Portions((EDamageType.Physical, 20), (EDamageType.Fire, 80)), 0);

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

            var dealt = context.DamageTarget(20, Portions((EDamageType.Fire, 2.0)), 0);

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

            context.DamageTarget(30, Single(EDamageType.Fire), 0);

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

            context.DamageTarget(30, Single(EDamageType.Fire), 0); // 30 × 1.5 = 45 dealt, but no applied vulnerability

            Assert.Equal(45, context.Stats.PlayerDamageDealt, 0.001);
            Assert.Equal(0, context.Stats.HexBonusDealt, 0.001);
        }

        [Fact]
        public void DamageTarget_AppliedVulnerability_BooksShareOfLandedDamage()
        {
            // The player debuffs the enemy's FireResistance by −0.5 (a vulnerability of v = 0.5), then hits for
            // raw 30: the hexed hit lands 45. The Hex share claim (#1481) is the booked (landed) 45 × φ(0.5) =
            // 45 × 1/3 = 15 — no counterfactual, just a φ-scaled share of what actually landed.
            var player = MakeBattlerWith((Endurance, 0));
            var enemy = MakeBattlerWith((Endurance, 0));
            var context = new BattleContext(player, enemy, timeDelta: 0, new Mulberry32(0));
            context.ApplySkillEffect(Vulnerability(FireResistance, -0.5));

            context.DamageTarget(30, Single(EDamageType.Fire), 0); // 30 × (1 − (−0.5)) = 45 dealt

            Assert.Equal(45, context.Stats.PlayerDamageDealt, 0.001);
            Assert.Equal(45.0 / 3.0, context.Stats.HexBonusDealt, 0.001); // 45 × φ(0.5) = 15
        }

        [Fact]
        public void DamageTarget_HexBonus_ScalesWithTheLandedHit()
        {
            // Same applied vulnerability (v = 0.5) against a soft enemy (innate 0 → live −0.5, lands 45) and a
            // resistant one (innate 0.4 → live −0.1, lands 33). The share claim scales with what landed — 15 vs
            // 11 — a deliberate property of the #1481 share shape: per battle the booked basis sums to at most
            // the enemy's health pool, so the per-hit mitigation dependence washes out at the accrual level
            // (where the old per-hit "flat in base resistance" marginal guarantee used to live).
            var softContext = MakeHexContext(innateFireResistance: 0);
            var resistantContext = MakeHexContext(innateFireResistance: 0.4);

            softContext.DamageTarget(30, Single(EDamageType.Fire), 0);
            resistantContext.DamageTarget(30, Single(EDamageType.Fire), 0);

            Assert.Equal(45.0 / 3.0, softContext.Stats.HexBonusDealt, 0.001);
            Assert.Equal(33.0 / 3.0, resistantContext.Stats.HexBonusDealt, 0.001);
        }

        [Fact]
        public void DamageTarget_HeavilyInvestedVulnerability_ClaimsAtMostTheBookedHit()
        {
            // A crushing vulnerability (v = 10) drives the hit to 330, but φ(10) = 10/11 bounds the claim below
            // the booked hit itself: 330 × 10/11 = 300 — a monster debuff cannot claim more than the damage that
            // landed (mirrors the crit-share ceiling). MaxHealth 350 so the swing doesn't kill (the health-removed
            // cap has its own test below).
            var player = MakeBattlerWith((Endurance, 0));
            var enemy = MakeBattlerWith((Endurance, 0), (MaxHealth, 300));
            var context = new BattleContext(player, enemy, timeDelta: 0, new Mulberry32(0));
            context.ApplySkillEffect(Vulnerability(FireResistance, -10));

            context.DamageTarget(30, Single(EDamageType.Fire), 0); // 30 × 11 = 330 dealt

            Assert.Equal(330.0 * 10.0 / 11.0, context.Stats.HexBonusDealt, 0.001);
        }

        [Fact]
        public void DamageTarget_KillingHit_OverlayClaimIsCappedAtHealthRemoved()
        {
            // The share basis is the booked (health-capped, #1482) damage: a 120-damage hexed hit on the 50-HP
            // enemy books 50, so Hex claims 50 × φ(0.5) — overkill mints no overlay training either.
            var player = MakeBattlerWith((Endurance, 0));
            var enemy = MakeBattlerWith((Endurance, 0)); // MaxHealth 50
            var context = new BattleContext(player, enemy, timeDelta: 0, new Mulberry32(0));
            context.ApplySkillEffect(Vulnerability(FireResistance, -0.5));

            context.DamageTarget(80, Single(EDamageType.Fire), 0); // 80 × 1.5 = 120 dealt, 50 removed

            Assert.Equal(120, context.Stats.PlayerDamageDealt, 0.001);
            Assert.Equal(50.0 / 3.0, context.Stats.HexBonusDealt, 0.001);
        }

        [Fact]
        public void DamageTarget_AbsorbedHit_BooksNoOverlayClaim()
        {
            // An absorbed hit (live resistance still above 1 despite the debuff) heals the enemy — nothing
            // landed, so the share basis is 0 and no overlay trains on it.
            var player = MakeBattlerWith((Endurance, 0));
            var enemy = MakeBattlerWith((Endurance, 0), (FireResistance, 2.0));
            var context = new BattleContext(player, enemy, timeDelta: 0, new Mulberry32(0));
            context.ApplySkillEffect(Vulnerability(FireResistance, -0.3)); // live 1.7, still absorbing
            context.DamageTarget(30, Single(EDamageType.Physical), 0); // 50 → 20, room for the heal

            context.DamageTarget(30, Single(EDamageType.Fire), 0); // 30 × (1 − 1.7) = −21, absorbed

            Assert.Equal(0, context.Stats.HexBonusDealt, 0.001);
        }

        [Fact]
        public void DamageTarget_CritAndHex_ClaimSharesOfTheSameLandedHit()
        {
            // Under the share shape both overlays claim off the same booked hit: v = 0.5 and a ×2 crit land 90,
            // so Hex claims 90 × φ(0.5) = 30 and Precision claims 90 × φ(1) = 45. The synergy pays through the
            // landed damage itself — bounded per battle by the enemy's health pool — while each φ stays on its
            // own investment (a crit cannot change v, nor the debuff m). MaxHealth 100 so the swing doesn't kill.
            var player = MakeBattlerWith((CriticalDamage, 0.5)); // CriticalDamage 2
            var enemy = MakeBattlerWith((Endurance, 0), (MaxHealth, 50));
            var context = new BattleContext(player, enemy, timeDelta: 0, new Mulberry32(0));
            context.ApplySkillEffect(Vulnerability(FireResistance, -0.5));

            context.DamageTarget(30, Single(EDamageType.Fire), 1); // hexed non-crit 45, crit ×2 = 90 dealt

            Assert.Equal(90.0 / 3.0, context.Stats.HexBonusDealt, 0.001); // 30
            Assert.Equal(90.0 * 0.5, context.Stats.CriticalBonusDealt, 0.001); // 45
        }

        [Fact]
        public void DamageTarget_VulnerabilityCrossesAbsorptionToDamage_ClaimsShareOfTheLandedHit()
        {
            // The crossover edge: the enemy innately absorbs Fire (FireResistance 1.5 > 1), and the player's −0.8
            // debuff brings live resistance to 0.7 so the hit lands +9. The share claim reads only what landed —
            // 9 × φ(0.8) = 4 — the old marginal's extra credit for the heal it prevented is deliberately gone
            // with #1481 (a share claim never runs a counterfactual); pinned so the crossover cannot drift.
            var player = MakeBattlerWith((Endurance, 0));
            var enemy = MakeBattlerWith((Endurance, 0), (FireResistance, 1.5));
            var context = new BattleContext(player, enemy, timeDelta: 0, new Mulberry32(0));
            context.ApplySkillEffect(Vulnerability(FireResistance, -0.8)); // live FireResistance 0.7, v = 0.8

            context.DamageTarget(30, Single(EDamageType.Fire), 0); // 30 × (1 − 0.7) = 9 dealt

            Assert.Equal(9, context.Stats.PlayerDamageDealt, 0.001);
            Assert.Equal(9.0 * 0.8 / 1.8, context.Stats.HexBonusDealt, 0.001); // 4
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

            context.DamageTarget(30, Single(EDamageType.Fire), 0); // 30 × (1 − 0.5) = 15 dealt, no applied vulnerability

            Assert.Equal(15, context.Stats.PlayerDamageDealt, 0.001);
            Assert.Equal(0, context.Stats.HexBonusDealt, 0.001);
        }

        [Fact]
        public void DamageTarget_EnemySelfBuffThenPlayerDebuff_CreditsThePlayersGrossContribution()
        {
            // Both move the same resistance: the enemy self-buffs +0.5 (untracked), the player debuffs −0.8
            // (tracked as v = 0.8). Hex credits the player's GROSS debuff — the resistance its debuff removed —
            // not the net reduction, so the enemy hardening itself lowers the landed basis but never the tracked
            // investment: the claim is the landed 39 × φ(0.8), not 39 × φ(0.3).
            var player = MakeBattlerWith((Endurance, 0));
            var enemy = MakeBattlerWith((Endurance, 0));
            var context = new BattleContext(player, enemy, timeDelta: 0, new Mulberry32(0));
            context.SwapActiveAndTargetBattlers();
            context.ApplySkillEffect(SelfResistanceBuff(FireResistance, 0.5)); // enemy: FireResistance 0 → 0.5
            context.SwapActiveAndTargetBattlers();
            context.ApplySkillEffect(Vulnerability(FireResistance, -0.8)); // player: 0.5 → −0.3, gross v = 0.8

            context.DamageTarget(30, Single(EDamageType.Fire), 0); // 30 × (1 − (−0.3)) = 39 dealt

            Assert.Equal(39, context.Stats.PlayerDamageDealt, 0.001);
            Assert.Equal(39.0 * 0.8 / 1.8, context.Stats.HexBonusDealt, 0.001); // ≈ 17.33
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

            context.DamageTarget(30, Single(EDamageType.Fire), 0); // resistance back to 0 ⇒ 30 dealt, no vulnerability

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

            context.DamageTarget(30, Single(EDamageType.Fire), 0);

            Assert.Equal(0, context.Stats.HexBonusDealt, 0.001);
        }

        [Fact]
        public void ResolveDamageOverTime_AppliedVulnerability_BooksHexBonusForTheEnemyDot()
        {
            // The player applies a Bleed DoT (100/s) and a Bleed vulnerability (−0.5 BleedResistance) to the
            // enemy, then a 1s tick resolves. The tick lands 100 × (1 − (−0.5)) = 150, and the Hex share claim
            // is the same shape as a direct hit's (#1481): the booked tick × φ(0.5) = 150 × 1/3 = 50.
            // MaxHealth 200 so the tick doesn't kill (the killing tick's booking cap is pinned in the DoT suite).
            var player = MakeBattlerWith((Endurance, 0));
            var enemy = MakeBattlerWith((Endurance, 0), (MaxHealth, 150));
            var context = new BattleContext(player, enemy, timeDelta: 1000, new Mulberry32(0));
            context.ApplySkillEffect(Vulnerability(BleedResistance, -0.5));
            context.ApplySkillEffect(Dot(BleedDamagePerSecond, 100));

            context.ResolveDamageOverTime();

            Assert.Equal(150, context.Stats.PlayerDamageDealt, 0.001);
            Assert.Equal(150.0 / 3.0, context.Stats.HexBonusDealt, 0.001); // 150 × φ(0.5) = 50
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

            context.DamageTarget(30, Single(EDamageType.Fire), 0);

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

            context.DamageTarget(30, Single(EDamageType.Fire), 0); // 30 × 1.5 = 45 dealt, but no applied ramp

            Assert.Equal(45, context.Stats.PlayerDamageDealt, 0.001);
            Assert.Equal(0, context.Stats.MomentumBonusDealt, 0.001);
        }

        [Fact]
        public void DamageTarget_AppliedRamp_BooksShareOfLandedDamage()
        {
            // The player ramps its own FireAmplification by +0.5, then hits for raw 30: the ramped hit lands 45.
            // The Momentum share claim (#1481) is the booked (landed) 45 × φ(0.5) = 15 — no un-ramped
            // counterfactual, just a φ-scaled share of what actually landed.
            var player = MakeBattlerWith((Endurance, 0));
            var enemy = MakeBattlerWith((Endurance, 0));
            var context = new BattleContext(player, enemy, timeDelta: 0, new Mulberry32(0));
            context.ApplySkillEffect(Ramp(FireAmplification, 0.5));

            context.DamageTarget(30, Single(EDamageType.Fire), 0); // 30 × (1 + 0.5) = 45 dealt

            Assert.Equal(45, context.Stats.PlayerDamageDealt, 0.001);
            Assert.Equal(45.0 / 3.0, context.Stats.MomentumBonusDealt, 0.001); // 45 × φ(0.5) = 15
        }

        [Fact]
        public void DamageTarget_HeavilyInvestedRamp_ClaimsAtMostTheBookedHit()
        {
            // A towering ramp (r = 10) drives the hit to 330, but φ(10) = 10/11 bounds the claim below the booked
            // hit itself: 330 × 10/11 = 300 — a monster stack cannot claim more than the damage that landed
            // (mirrors the crit and Hex ceilings). MaxHealth 350 so the swing doesn't kill.
            var player = MakeBattlerWith((Endurance, 0));
            var enemy = MakeBattlerWith((Endurance, 0), (MaxHealth, 300));
            var context = new BattleContext(player, enemy, timeDelta: 0, new Mulberry32(0));
            context.ApplySkillEffect(Ramp(FireAmplification, 10));

            context.DamageTarget(30, Single(EDamageType.Fire), 0); // 30 × 11 = 330 dealt

            Assert.Equal(330.0 * 10.0 / 11.0, context.Stats.MomentumBonusDealt, 0.001);
        }

        [Fact]
        public void DamageTarget_MomentumBonus_IsMitigatedLikeTheRestOfTheHit()
        {
            // The share basis is the landed damage, so the defender's mitigation flows through: a 50% resistant
            // enemy halves the claim too (22.5 × φ(0.5) = 7.5, half the unresisted 15). Per battle this washes
            // out — the booked basis sums to at most the enemy's health pool either way (#1481).
            var player = MakeBattlerWith((Endurance, 0));
            var enemy = MakeBattlerWith((Endurance, 0), (FireResistance, 0.5));
            var context = new BattleContext(player, enemy, timeDelta: 0, new Mulberry32(0));
            context.ApplySkillEffect(Ramp(FireAmplification, 0.5));

            context.DamageTarget(30, Single(EDamageType.Fire), 0); // 45 × (1 − 0.5) = 22.5 dealt

            Assert.Equal(22.5, context.Stats.PlayerDamageDealt, 0.001);
            Assert.Equal(22.5 / 3.0, context.Stats.MomentumBonusDealt, 0.001); // 7.5
        }

        [Fact]
        public void DamageTarget_CritAndMomentum_ClaimSharesOfTheSameLandedHit()
        {
            // Both overlays claim off the same booked hit: r = 0.5 and a ×2 crit land 90, so Momentum claims
            // 90 × φ(0.5) = 30 and Precision claims 90 × φ(1) = 45 — the synergy pays through the landed damage
            // (HP-bounded per battle), each φ staying on its own investment. MaxHealth 100 so no kill.
            var player = MakeBattlerWith((CriticalDamage, 0.5)); // CriticalDamage 2
            var enemy = MakeBattlerWith((Endurance, 0), (MaxHealth, 50));
            var context = new BattleContext(player, enemy, timeDelta: 0, new Mulberry32(0));
            context.ApplySkillEffect(Ramp(FireAmplification, 0.5));

            context.DamageTarget(30, Single(EDamageType.Fire), 1); // ramped non-crit 45, crit ×2 = 90 dealt

            Assert.Equal(90.0 / 3.0, context.Stats.MomentumBonusDealt, 0.001); // 30
            Assert.Equal(90.0 * 0.5, context.Stats.CriticalBonusDealt, 0.001); // 45
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

            context.DamageTarget(30, Single(EDamageType.Fire), 0); // amplification back to 0 ⇒ 30 dealt, no ramp

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

            context.DamageTarget(30, Single(EDamageType.Fire), 0);

            Assert.Equal(0, context.Stats.MomentumBonusDealt, 0.001);
        }

        // ── DamageTarget: Cull execute tally (#1430) ──────────────────────────

        [Fact]
        public void DamageTarget_NoExecuteBonus_DealsUnmultipliedDamageAndBooksNoCullBonus()
        {
            // A damaged target (40% missing) enables nothing without an authored ExecuteBonus — the real damage
            // stays unmultiplied and Cull trains on nothing.
            var player = MakeBattlerWith((Endurance, 0));
            var enemy = MakeBattlerWith((Endurance, 0)); // MaxHealth 50, Toughness 0
            enemy.TakeReflectedDamage(20); // CurrentHealth 30 (40% missing)
            var context = new BattleContext(player, enemy, timeDelta: 0, new Mulberry32(0));

            context.DamageTarget(10, Single(EDamageType.Physical), 0);

            Assert.Equal(10, context.Stats.PlayerDamageDealt, 0.001);
            Assert.Equal(0, context.Stats.CullBonusDealt, 0.001);
        }

        [Fact]
        public void DamageTarget_ExecuteBonusAtFullHealth_DealsUnmultipliedDamageAndBooksNoCullBonus()
        {
            // ExecuteBonus is authored, but the target is at full health, so the missing-HP fraction is 0 and
            // the multiplier is exactly 1 — no bonus, no training.
            var player = MakeBattlerWith((Endurance, 0), (ExecuteBonus, 1.0));
            var enemy = MakeBattlerWith((Endurance, 0)); // full health
            var context = new BattleContext(player, enemy, timeDelta: 0, new Mulberry32(0));

            context.DamageTarget(10, Single(EDamageType.Physical), 0);

            Assert.Equal(10, context.Stats.PlayerDamageDealt, 0.001);
            Assert.Equal(0, context.Stats.CullBonusDealt, 0.001);
        }

        [Fact]
        public void DamageTarget_ExecuteBonusAgainstDamagedTarget_MultipliesRealDamageAndBooksShareOfLandedDamage()
        {
            // The target is missing 40% of its health (20/50), so a full (100%) ExecuteBonus scales this fire's
            // multiplier to 1 + 1.0×0.4 = 1.4: 10 raw × 1.4 = 14 dealt. The Cull share claim (#1481) is the
            // booked (landed) 14 × φ(0.4) = 14 × 0.4/1.4 = 4.
            var player = MakeBattlerWith((Endurance, 0), (ExecuteBonus, 1.0));
            var enemy = MakeBattlerWith((Endurance, 0)); // MaxHealth 50, Toughness 0
            enemy.TakeReflectedDamage(20); // CurrentHealth 30 (40% missing)
            var context = new BattleContext(player, enemy, timeDelta: 0, new Mulberry32(0));

            context.DamageTarget(10, Single(EDamageType.Physical), 0);

            Assert.Equal(50 - 20 - 14, enemy.CurrentHealth, 0.001);
            Assert.Equal(14, context.Stats.PlayerDamageDealt, 0.001);
            Assert.Equal(14.0 * 0.4 / 1.4, context.Stats.CullBonusDealt, 0.001); // 4
        }

        [Fact]
        public void DamageTarget_HeavilyInvestedExecuteAgainstNearDeadTarget_ClaimsAtMostTheHealthRemoved()
        {
            // A target at 2% health (1/50) with a towering ExecuteBonus (50 = 5000%) drives the multiplier to
            // 1 + 50×0.98 = 50: 20 raw × 50 = 1000 dealt — but only 1 health existed to remove, so the booked
            // basis is 1 (#1482) and the claim is 1 × φ(49) = 0.98. The execute archetype is the natural
            // overkill machine; the health-removed basis is exactly what stops execute one-shots from minting
            // Cull training out of damage that hit a corpse.
            var player = MakeBattlerWith((Endurance, 0), (ExecuteBonus, 50.0));
            var enemy = MakeBattlerWith((Endurance, 0)); // MaxHealth 50, Toughness 0
            enemy.TakeReflectedDamage(49); // CurrentHealth 1 (98% missing)
            var context = new BattleContext(player, enemy, timeDelta: 0, new Mulberry32(0));

            context.DamageTarget(20, Single(EDamageType.Physical), 0);

            Assert.Equal(1000, context.Stats.PlayerDamageDealt, 0.001);
            Assert.Equal(1.0 * 49.0 / 50.0, context.Stats.CullBonusDealt, 0.001); // 0.98
        }

        [Fact]
        public void DamageTarget_CritAndCull_ClaimSharesOfTheSameLandedHit()
        {
            // Both overlays claim off the same booked hit. The real damage composes both multipliers —
            // 10 × 2 (crit) × 1.4 (execute) = 28 — so Cull claims 28 × φ(0.4) = 8 and Precision claims
            // 28 × φ(1) = 14, each φ on its own investment.
            var player = MakeBattlerWith(
                (Endurance, 0), (CriticalDamage, 0.5), (ExecuteBonus, 1.0)); // CriticalDamage 2
            var enemy = MakeBattlerWith((Endurance, 0)); // MaxHealth 50, Toughness 0
            enemy.TakeReflectedDamage(20); // CurrentHealth 30 (40% missing)
            var context = new BattleContext(player, enemy, timeDelta: 0, new Mulberry32(0));

            context.DamageTarget(10, Single(EDamageType.Physical), 1);

            Assert.Equal(28, context.Stats.PlayerDamageDealt, 0.001);
            Assert.Equal(28.0 * 0.4 / 1.4, context.Stats.CullBonusDealt, 0.001); // 8
            Assert.Equal(28.0 * 0.5, context.Stats.CriticalBonusDealt, 0.001); // 14
        }

        [Fact]
        public void DamageTarget_EnemyAttacking_IgnoresEnemyExecuteBonusAndBooksNoCullBonus()
        {
            // Cull is a player-offense signal, like Hex/Momentum. Even if the enemy carries ExecuteBonus and the
            // player is itself damaged, an enemy hit is never multiplied by it — the whole mechanic is gated on
            // the player-active branch — so the player takes exactly the raw 10 and Cull trains on nothing.
            var player = MakeBattlerWith((Endurance, 0)); // MaxHealth 50, Toughness 0
            player.TakeReflectedDamage(20); // CurrentHealth 30 (40% missing)
            var enemy = MakeBattlerWith((Endurance, 0), (ExecuteBonus, 1.0));
            var context = new BattleContext(player, enemy, timeDelta: 0, new Mulberry32(0));
            context.SwapActiveAndTargetBattlers(); // enemy attacks

            context.DamageTarget(10, Single(EDamageType.Physical), 0);

            Assert.Equal(30 - 10, player.CurrentHealth, 0.001);
            Assert.Equal(0, context.Stats.CullBonusDealt, 0.001);
        }

        // ── DamageTarget: Sunder mitigation tally (#1429) ─────────────────────

        [Fact]
        public void DamageTarget_NoSunderApplied_BooksNoSunderBonus()
        {
            // A hit against an un-debuffed enemy enables no Toughness-curve bypass, so Sunder trains on nothing.
            var player = MakeBattlerWith((Endurance, 0));
            var enemy = MakeBattlerWith((Endurance, 10)); // Toughness 20
            var context = new BattleContext(player, enemy, timeDelta: 0, new Mulberry32(0));

            context.DamageTarget(30, Single(EDamageType.Physical), 0);

            Assert.Equal(0, context.Stats.SunderBonusDealt, 0.001);
        }

        [Fact]
        public void DamageTarget_AppliedSunder_BooksLandedDamageScaledByInvestment()
        {
            // Sunder books the landed (booked) damage × φ(investment) — the share-claim shape every overlay uses
            // (#1481). Attacker (player) level 1 ⇒ K·level = 20; a −20 debuff (s = 20) gives investment
            // 20/20 = 1.0, φ(1.0) = 0.5. The debuff strips the enemy's Toughness 20 to 0, so the hit lands the
            // full 30 and the claim is 30 × 0.5 = 15.
            var player = MakeBattlerWith((Endurance, 0));
            var enemy = MakeBattlerWith((Endurance, 10)); // Toughness 20
            var context = new BattleContext(player, enemy, timeDelta: 0, new Mulberry32(0));
            context.ApplySkillEffect(SunderDebuff(-20));

            context.DamageTarget(30, Single(EDamageType.Physical), 0); // Toughness 0 ⇒ no curve ⇒ 30 dealt

            Assert.Equal(30, context.Stats.PlayerDamageDealt, 0.001);
            Assert.Equal(15, context.Stats.SunderBonusDealt, 0.001);
        }

        [Fact]
        public void DamageTarget_PartialSunder_BooksSmallerProportionalBonus()
        {
            // A lighter debuff (−10, half of the previous case) gives half the investment (10/20 = 0.5),
            // φ(0.5) = 1/3 — and the half-stripped Toughness (10) also mitigates the hit to 20 landed, so the
            // claim is 20/3 ≈ 6.667 — smaller investment, smaller share of a smaller landed hit
            // (strength-proportionality lives in φ; the basis is what actually landed).
            var player = MakeBattlerWith((Endurance, 0));
            var enemy = MakeBattlerWith((Endurance, 10)); // Toughness 20
            var context = new BattleContext(player, enemy, timeDelta: 0, new Mulberry32(0));
            context.ApplySkillEffect(SunderDebuff(-10)); // live Toughness 10, live reduction 1/3

            context.DamageTarget(30, Single(EDamageType.Physical), 0); // 30 × (1 − 1/3) = 20 dealt

            Assert.Equal(20, context.Stats.PlayerDamageDealt, 0.001);
            Assert.Equal(20.0 / 3.0, context.Stats.SunderBonusDealt, 0.001);
        }

        [Fact]
        public void DamageTarget_HeavilyInvestedSunder_SaturatesTowardTheLandedHit()
        {
            // A towering debuff (s = 180) drives the investment to 180/20 = 9, φ(9) = 0.9, so the claim
            // approaches (but never reaches) the landed hit. The enemy's own Toughness (Endurance 90 → 180)
            // exactly cancels the debuff to the 0 boundary — the curve's unfloored pole at -20 is deliberately
            // left unguarded (#1478), so this pins the boundary rather than crossing into it: the hit lands the
            // full 30 and the tally banks 30 × 0.9 = 27 — the same ceiling shape as the other overlays.
            var player = MakeBattlerWith((Endurance, 0));
            var enemy = MakeBattlerWith((Endurance, 90));
            var context = new BattleContext(player, enemy, timeDelta: 0, new Mulberry32(0));
            context.ApplySkillEffect(SunderDebuff(-180));

            context.DamageTarget(30, Single(EDamageType.Physical), 0);

            Assert.Equal(27, context.Stats.SunderBonusDealt, 0.001);
        }

        [Fact]
        public void DamageTarget_SunderBonus_ScalesWithTheLandedHit()
        {
            // The investment (φ side) reads only the player's own debuff and level, but the basis is the landed
            // hit — so the same −10 debuff claims 20/3 against a soft enemy (Toughness 20 → 10, lands 20) and far
            // less against a resistant fortress (Toughness 400 → 390, 50% resist ⇒ lands ≈ 0.73). Deliberate
            // under the #1481 share shape: per battle the booked basis sums to at most the enemy's health pool,
            // so the per-hit difference washes out at the accrual level (where the old per-hit
            // enemy-independence guarantee used to live).
            var softPlayer = MakeBattlerWith((Endurance, 0));
            var softEnemy = MakeBattlerWith((Endurance, 10)); // Toughness 20, no resistance
            var softContext = new BattleContext(softPlayer, softEnemy, timeDelta: 0, new Mulberry32(0));
            softContext.ApplySkillEffect(SunderDebuff(-10));

            var toughPlayer = MakeBattlerWith((Endurance, 0));
            var toughEnemy = MakeBattlerWith((Endurance, 200), (PhysicalResistance, 0.5)); // Toughness 400, 50% resist
            var toughContext = new BattleContext(toughPlayer, toughEnemy, timeDelta: 0, new Mulberry32(0));
            toughContext.ApplySkillEffect(SunderDebuff(-10));

            softContext.DamageTarget(30, Single(EDamageType.Physical), 0);
            toughContext.DamageTarget(30, Single(EDamageType.Physical), 0);

            Assert.Equal(20.0 / 3.0, softContext.Stats.SunderBonusDealt, 0.001);
            // Tough enemy: 30 × 0.5 resisted = 15, × (1 − 390/410) through the curve = 15 × 20/410 landed, × φ(0.5).
            Assert.Equal(15.0 * 20.0 / 410.0 / 3.0, toughContext.Stats.SunderBonusDealt, 0.001);
            Assert.NotEqual(softContext.Stats.PlayerDamageDealt, toughContext.Stats.PlayerDamageDealt);
        }

        [Fact]
        public void DamageTarget_CritAndSunder_ClaimSharesOfTheSameLandedHit()
        {
            // Both overlays claim off the same booked hit: the sundered (Toughness 0) crit lands 60, so Sunder
            // claims 60 × φ(1.0) = 30 and Precision claims 60 × φ(1) = 30 — each φ on its own investment.
            var player = MakeBattlerWith((CriticalDamage, 0.5)); // CriticalDamage 2
            var enemy = MakeBattlerWith((Endurance, 10)); // Toughness 20, MaxHealth 250
            var context = new BattleContext(player, enemy, timeDelta: 0, new Mulberry32(0));
            context.ApplySkillEffect(SunderDebuff(-20));

            context.DamageTarget(30, Single(EDamageType.Physical), 1); // sundered Toughness 0, crit ×2 = 60 dealt

            Assert.Equal(60, context.Stats.PlayerDamageDealt, 0.001);
            Assert.Equal(30, context.Stats.SunderBonusDealt, 0.001); // 60 × φ(1.0)
            Assert.Equal(30, context.Stats.CriticalBonusDealt, 0.001); // 60 × φ(m−1)
        }

        [Fact]
        public void DamageTarget_SunderExpired_BooksNoSunderBonus()
        {
            // The tracked Sunder debuff rides the effect stack's shared expiry: once it lapses the stack (and its
            // contribution) is gone, so a later hit trains no Sunder and lands at full Toughness again.
            var player = MakeBattlerWith((Endurance, 0));
            var enemy = MakeBattlerWith((Endurance, 10));
            var context = new BattleContext(player, enemy, timeDelta: 0, new Mulberry32(0));
            context.ApplySkillEffect(SunderDebuff(-20)); // DurationMs 10_000
            enemy.AdvanceEffects(10_001); // past expiry ⇒ the Toughness stack and its contribution are removed

            context.DamageTarget(30, Single(EDamageType.Physical), 0); // Toughness back to 20 ⇒ 15 dealt, no debuff

            Assert.Equal(15, context.Stats.PlayerDamageDealt, 0.001);
            Assert.Equal(0, context.Stats.SunderBonusDealt, 0.001);
        }

        [Fact]
        public void DamageTarget_EnemyAttacking_BooksNoSunderBonus()
        {
            // Sunder is a player-offense signal. Even if the player is somehow sundered, an enemy hit never
            // trains the player's Sunder — only the player-active branch accrues it.
            var player = MakeBattlerWith((Endurance, 10));
            var enemy = MakeBattlerWith((Endurance, 0));
            var context = new BattleContext(player, enemy, timeDelta: 0, new Mulberry32(0));
            context.SwapActiveAndTargetBattlers();
            context.ApplySkillEffect(SunderDebuff(-20)); // lands on the player while the enemy is active

            context.DamageTarget(30, Single(EDamageType.Physical), 0);

            Assert.Equal(0, context.Stats.SunderBonusDealt, 0.001);
        }

        [Fact]
        public void ResolveDamageOverTime_AppliedSunder_BooksNoSunderBonus()
        {
            // DoT bypasses the Toughness curve entirely, so a Toughness debuff cannot affect it — no marginal to
            // credit, unlike Hex's resistance debuff which does have a DoT counterpart.
            var player = MakeBattlerWith((Endurance, 0));
            var enemy = MakeBattlerWith((Endurance, 10));
            var context = new BattleContext(player, enemy, timeDelta: 1000, new Mulberry32(0));
            context.ApplySkillEffect(SunderDebuff(-20));
            context.ApplySkillEffect(Dot(BleedDamagePerSecond, 100));

            context.ResolveDamageOverTime();

            Assert.Equal(100, context.Stats.PlayerDamageDealt, 0.001); // unaffected by the Toughness debuff
            Assert.Equal(0, context.Stats.SunderBonusDealt, 0.001);
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

        private static double Mitigated(BattleContext context, EDamageType type) =>
            context.Stats.TypedDamageResistanceMitigated.TryGetValue(type, out var value) ? value : 0;

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

        // A player-cast Opponent-targeted Toughness debuff (negative additive) — the Sunder enabler (#1429).
        // Applied via BattleContext.ApplySkillEffect so it lands on the target (enemy) battler.
        private static SkillEffect SunderDebuff(double amount) => new()
        {
            Id = 5,
            Target = ESkillEffectTarget.Opponent,
            AttributeId = EAttribute.Toughness,
            ModifierType = EModifierType.Additive,
            Amount = amount,
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

        // A bare skill for the Parry (#1457) counter-fire tests: single Physical portion, no multipliers/effects.
        private static Skill MakeCounterSkill(double baseDamage, double criticalChance = 0) => new()
        {
            Id = 900,
            Name = "Counter Skill",
            Description = "",
            DamagePortions = Single(EDamageType.Physical),
            CooldownMs = 1000,
            BaseDamage = baseDamage,
            CriticalChance = criticalChance,
            DamageMultipliers = [],
            Effects = [],
        };

        // Builds a player Battler through the snapshot path with a single equipped weapon whose granted
        // signature is <paramref name="counterSkill"/>, so Battler.CounterSkill resolves it — the Parry (#1457)
        // riposte source — exactly like BattleSnapshot.ToBattler resolves it in production.
        private static Battler MakeBattlerWithCounter(Skill counterSkill, params (EAttribute Attribute, double Amount)[] attributes)
        {
            var weapon = new Item
            {
                Id = 1,
                Name = "Weapon",
                Description = string.Empty,
                Category = EItemCategory.Weapon,
                Rarity = ERarity.Common,
                WeaponType = EDamageType.Physical,
                GrantedSkillId = counterSkill.Id,
                Attributes = [],
                ModSlots = [],
            };
            var snapshot = new BattleSnapshot
            {
                Level = 1,
                StatAllocations = attributes.Select(a => new StatAllocation { Attribute = a.Attribute, Amount = a.Amount }).ToList(),
                EquippedItems = [new EquippedItemSnapshot { ItemId = weapon.Id, AppliedModIds = [] }],
                SkillIds = [],
            };

            return snapshot.ToBattler(
                _ => weapon,
                _ => throw new InvalidOperationException("No mods are applied in the parry scenarios."),
                id => id == counterSkill.Id ? counterSkill : null);
        }
    }
}
