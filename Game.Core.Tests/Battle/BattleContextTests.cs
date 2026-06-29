using Game.Core;
using Game.Core.Battle;
using Game.Core.Players;
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

            context.DamageTarget(20, EDamageType.Physical); // crit ⇒ 20×2 = 40; no Toughness ⇒ 40 dealt

            Assert.Equal(50 - 40, enemy.CurrentHealth, 0.001);
            Assert.Equal(40, context.Stats.PlayerDamageDealt, 0.001);
        }

        [Fact]
        public void DamageTarget_PlayerNoCrit_DealsRawUnmitigated()
        {
            var player = MakeBattlerWith((CriticalChance, 0), (CriticalDamage, 2));
            var enemy = MakeBattlerWith((Endurance, 0)); // Toughness 0
            var context = new BattleContext(player, enemy, timeDelta: 0, new Mulberry32(0));

            context.DamageTarget(20, EDamageType.Physical); // no crit ⇒ 20, no Toughness ⇒ 20

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

            context.DamageTarget(20, EDamageType.Physical); // dodged ⇒ 0

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

            context.DamageTarget(20, EDamageType.Physical);

            Assert.Equal(before - 20, player.CurrentHealth, 0.001);
        }

        // ── DamageTarget: crit/dodge/block statistics ────────────────────────

        [Fact]
        public void DamageTarget_PlayerCrit_RecordsCritHitAndPostMitigationDamage()
        {
            var player = MakeBattlerWith((CriticalChance, 1), (CriticalDamage, 0.5)); // CriticalDamage 2
            var enemy = MakeBattlerWith((Endurance, 0)); // Toughness 0
            var context = new BattleContext(player, enemy, timeDelta: 0, new Mulberry32(0));

            context.DamageTarget(20, EDamageType.Physical); // 20×2 = 40, no Toughness ⇒ 40 dealt

            Assert.Equal(1, context.Stats.CriticalHits);
            Assert.Equal(40, context.Stats.CriticalDamageDealt, 0.001);
        }

        [Fact]
        public void DamageTarget_PlayerNoCrit_RecordsNoCritStatistics()
        {
            var player = MakeBattlerWith((CriticalChance, 0), (CriticalDamage, 2));
            var enemy = MakeBattlerWith((Endurance, 0));
            var context = new BattleContext(player, enemy, timeDelta: 0, new Mulberry32(0));

            context.DamageTarget(20, EDamageType.Physical);

            Assert.Equal(0, context.Stats.CriticalHits);
            Assert.Equal(0, context.Stats.CriticalDamageDealt, 0.001);
        }

        [Fact]
        public void DamageTarget_PlayerDodge_RecordsDodgeAndPostMitigationDamageAvoided()
        {
            var player = MakeBattlerWith((DodgeChance, 1)); // Toughness 0
            var enemy = MakeBattlerWith((Endurance, 0));
            var context = new BattleContext(player, enemy, timeDelta: 0, new Mulberry32(0));
            context.SwapActiveAndTargetBattlers();

            context.DamageTarget(20, EDamageType.Physical); // would deal 20 (no Toughness), fully avoided

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

            context.DamageTarget(20, EDamageType.Physical);

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

            context.DamageTarget(5, EDamageType.Physical);               // player attacking → 1 draw
            context.SwapActiveAndTargetBattlers();
            context.DamageTarget(5, EDamageType.Physical);               // enemy attacking → 1 draw

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

            var dealt = context.DamageTarget(40, EDamageType.Physical); // 40 to the enemy

            Assert.Equal(40, dealt, 0.001);
            Assert.Equal(playerBefore - 20, player.CurrentHealth, 0.001); // 40 × 0.5, unmitigated
            Assert.Equal(40, context.Stats.PlayerDamageDealt, 0.001);     // just the hit; reflection is not the player's damage dealt here
            Assert.Equal(20, context.Stats.PlayerDamageTaken, 0.001);     // the enemy reflected 20 onto the player
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

            context.DamageTarget(50, EDamageType.Physical);

            Assert.Equal(enemyBefore - 20, enemy.CurrentHealth, 0.001); // 50 × 0.4 reflected onto the enemy
            Assert.Equal(20, context.Stats.PlayerDamageDealt, 0.001);
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

            context.DamageTarget(50, EDamageType.Physical);

            Assert.Equal(enemyBefore, enemy.CurrentHealth, 0.001);
            Assert.Equal(0, context.Stats.PlayerDamageDealt, 0.001);
        }

        [Fact]
        public void DamageTarget_AbsorbedHit_ReflectsNothing()
        {
            // An absorbed hit (resistance > 1) is a net heal, not positive damage, so nothing is reflected even
            // with reflection authored on the defender. A prior physical hit makes room for the absorption heal.
            var player = MakeBattlerWith((Endurance, 0), (DamageReflection, 1.0)); // also reflects, to prove it stays 0
            var enemy = MakeBattlerWith((Endurance, 0), (FireResistance, 2.0));
            var context = new BattleContext(player, enemy, timeDelta: 0, new Mulberry32(0));
            context.DamageTarget(30, EDamageType.Physical); // 30 to the enemy → CurrentHealth 20
            var playerBefore = player.CurrentHealth;

            context.DamageTarget(20, EDamageType.Fire); // 20 × (1 − 2) = −20, absorbed heal — no reflection

            Assert.Equal(playerBefore, player.CurrentHealth, 0.001); // the player took no reflected damage
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

            context.DamageTarget(40, EDamageType.Fire);

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

            context.DamageTarget(40, EDamageType.Fire);

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
            context.DamageTarget(30, EDamageType.Physical); // 30 (no Toughness) → CurrentHealth 20

            context.DamageTarget(20, EDamageType.Fire);

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

            context.DamageTarget(20, EDamageType.Fire);

            Assert.Equal(50 - 10, enemy.CurrentHealth, 0.001);
        }

        // ── DamageTarget: typed damage dealt (offense book, #1337) ───────────

        [Fact]
        public void DamageTarget_PlayerHit_RecordsTypedDamageDealtMatchingPlayerDamageDealt()
        {
            var player = MakeBattlerWith((Endurance, 0));
            var enemy = MakeBattlerWith((Endurance, 0)); // Toughness 0
            var context = new BattleContext(player, enemy, timeDelta: 0, new Mulberry32(0));

            context.DamageTarget(20, EDamageType.Fire); // no Toughness ⇒ 20 dealt

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

            context.DamageTarget(20, EDamageType.Fire);     // 20
            context.DamageTarget(10, EDamageType.Fire);     // 10
            context.DamageTarget(30, EDamageType.Physical); // 30

            Assert.Equal(30, TypedDealt(context, EDamageType.Fire), 0.001); // 20 + 10
            Assert.Equal(30, TypedDealt(context, EDamageType.Physical), 0.001);
        }

        [Fact]
        public void DamageTarget_PlayerCrit_TypedDamageDealtUsesPostMitigationActual()
        {
            var player = MakeBattlerWith((CriticalChance, 1), (CriticalDamage, 0.5)); // CriticalDamage 2
            var enemy = MakeBattlerWith((Endurance, 0)); // Toughness 0
            var context = new BattleContext(player, enemy, timeDelta: 0, new Mulberry32(0));

            context.DamageTarget(20, EDamageType.Physical); // 20×2 = 40, no Toughness ⇒ 40

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

            context.DamageTarget(20, EDamageType.Physical);

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

            context.DamageTarget(40, EDamageType.Fire);

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

            context.DamageTarget(40, EDamageType.Fire);

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

            context.DamageTarget(20, EDamageType.Physical);

            Assert.Empty(context.Stats.TypedDamageExposure);
        }

        [Fact]
        public void DamageTarget_PlayerActive_RecordsNoExposure()
        {
            // The incoming book is the player's exposure; the player's own hits never add to it.
            var player = MakeBattlerWith((Endurance, 0));
            var enemy = MakeBattlerWith((Endurance, 0));
            var context = new BattleContext(player, enemy, timeDelta: 0, new Mulberry32(0));

            context.DamageTarget(20, EDamageType.Physical);

            Assert.Empty(context.Stats.TypedDamageExposure);
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private static double TypedDealt(BattleContext context, EDamageType type) =>
            context.Stats.TypedDamageDealt.TryGetValue(type, out var value) ? value : 0;

        private static double Exposure(BattleContext context, EDamageType type) =>
            context.Stats.TypedDamageExposure.TryGetValue(type, out var value) ? value : 0;

        private static BattleContext MakeContext()
        {
            return new BattleContext(MakeBattler(), MakeBattler(), timeDelta: 0, new Mulberry32(0));
        }

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
