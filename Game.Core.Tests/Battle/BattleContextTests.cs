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
        public void DamageTarget_PlayerCrit_MultipliesRawBeforeDefense()
        {
            // CriticalChance 1 always succeeds; CriticalDamage is the base 1.5 + 0.5 = 2, read directly as the multiplier.
            var player = MakeBattlerWith((CriticalChance, 1), (CriticalDamage, 0.5));
            var enemy = MakeBattlerWith((Endurance, 0)); // MaxHealth 50, Defense 2
            var context = new BattleContext(player, enemy, timeDelta: 0, new Mulberry32(0));

            context.DamageTarget(20, EDamageType.Physical); // crit ⇒ 20×2 = 40, then −2 Defense = 38

            Assert.Equal(50 - 38, enemy.CurrentHealth, 0.001);
            Assert.Equal(38, context.Stats.PlayerDamageDealt, 0.001);
        }

        [Fact]
        public void DamageTarget_PlayerNoCrit_DealsRawMinusDefense()
        {
            var player = MakeBattlerWith((CriticalChance, 0), (CriticalDamage, 2));
            var enemy = MakeBattlerWith((Endurance, 0));
            var context = new BattleContext(player, enemy, timeDelta: 0, new Mulberry32(0));

            context.DamageTarget(20, EDamageType.Physical); // no crit ⇒ 20 − 2 Defense = 18

            Assert.Equal(50 - 18, enemy.CurrentHealth, 0.001);
        }

        // ── DamageTarget: enemy attacking the player (dodge / block) ──────────

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
        public void DamageTarget_PlayerBlocksEnemyHit_SubtractsDefenseAndBlockReduction()
        {
            // BlockReduction is the base 2 + 8 = 10; Defense 2.
            var player = MakeBattlerWith((BlockChance, 1), (BlockReduction, 8));
            var enemy = MakeBattlerWith((Endurance, 0));
            var context = new BattleContext(player, enemy, timeDelta: 0, new Mulberry32(0));
            context.SwapActiveAndTargetBattlers();
            var before = player.CurrentHealth;

            context.DamageTarget(20, EDamageType.Physical); // blocked ⇒ 20 − 2 Defense − 10 BlockReduction = 8

            Assert.Equal(before - 8, player.CurrentHealth, 0.001);
            Assert.Equal(8, context.Stats.PlayerDamageTaken, 0.001);
        }

        [Fact]
        public void DamageTarget_EnemyAttacking_NeverCrits()
        {
            // The enemy carries a forced crit, but the roll is gated on the player attacking, so the enemy's
            // hit lands un-multiplied (20 − 2 = 18, not 40 − 2 = 38).
            var player = MakeBattlerWith((Endurance, 0));
            var enemy = MakeBattlerWith((CriticalChance, 1), (CriticalDamage, 2));
            var context = new BattleContext(player, enemy, timeDelta: 0, new Mulberry32(0));
            context.SwapActiveAndTargetBattlers();
            var before = player.CurrentHealth;

            context.DamageTarget(20, EDamageType.Physical);

            Assert.Equal(before - 18, player.CurrentHealth, 0.001);
        }

        // ── DamageTarget: crit/dodge/block statistics ────────────────────────

        [Fact]
        public void DamageTarget_PlayerCrit_RecordsCritHitAndPostDefenseDamage()
        {
            var player = MakeBattlerWith((CriticalChance, 1), (CriticalDamage, 0.5)); // CriticalDamage 2
            var enemy = MakeBattlerWith((Endurance, 0)); // Defense 2
            var context = new BattleContext(player, enemy, timeDelta: 0, new Mulberry32(0));

            context.DamageTarget(20, EDamageType.Physical); // 20×2 = 40, −2 Defense = 38 dealt

            Assert.Equal(1, context.Stats.CriticalHits);
            Assert.Equal(38, context.Stats.CriticalDamageDealt, 0.001);
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
        public void DamageTarget_PlayerDodge_RecordsDodgeAndPostDefenseDamageAvoided()
        {
            var player = MakeBattlerWith((DodgeChance, 1)); // Defense 2
            var enemy = MakeBattlerWith((Endurance, 0));
            var context = new BattleContext(player, enemy, timeDelta: 0, new Mulberry32(0));
            context.SwapActiveAndTargetBattlers();

            context.DamageTarget(20, EDamageType.Physical); // would deal 20 − 2 Defense = 18, fully avoided

            Assert.Equal(1, context.Stats.AttacksDodged);
            Assert.Equal(18, context.Stats.DamageDodged, 0.001);
            Assert.Equal(0, context.Stats.AttacksBlocked);
        }

        [Fact]
        public void DamageTarget_PlayerBlock_RecordsBlockAndReductionPrevented()
        {
            // BlockReduction base 2 + 8 = 10; Defense 2.
            var player = MakeBattlerWith((BlockChance, 1), (BlockReduction, 8));
            var enemy = MakeBattlerWith((Endurance, 0));
            var context = new BattleContext(player, enemy, timeDelta: 0, new Mulberry32(0));
            context.SwapActiveAndTargetBattlers();

            context.DamageTarget(20, EDamageType.Physical); // after Defense 18, blocked to 8 ⇒ reduction prevented = 10

            Assert.Equal(1, context.Stats.AttacksBlocked);
            Assert.Equal(10, context.Stats.DamageBlocked, 0.001);
            Assert.Equal(0, context.Stats.AttacksDodged);
        }

        [Fact]
        public void DamageTarget_PlayerBlock_ReductionNeverExceedsTheWouldBeDamage()
        {
            // The block over-absorbs: after Defense only 3 damage remained, so the reduction prevented is 3,
            // not the full BlockReduction (the blocked amount can never exceed the would-be hit).
            var player = MakeBattlerWith((BlockChance, 1), (BlockReduction, 8)); // BlockReduction 10
            var enemy = MakeBattlerWith((Endurance, 0)); // Defense 2
            var context = new BattleContext(player, enemy, timeDelta: 0, new Mulberry32(0));
            context.SwapActiveAndTargetBattlers();

            context.DamageTarget(5, EDamageType.Physical); // after Defense 3, blocked to 0 ⇒ reduction prevented = 3

            Assert.Equal(1, context.Stats.AttacksBlocked);
            Assert.Equal(3, context.Stats.DamageBlocked, 0.001);
        }

        [Fact]
        public void DamageTarget_NormalEnemyHit_RecordsNoDodgeOrBlock()
        {
            var player = MakeBattlerWith((Endurance, 0)); // no dodge/block chance
            var enemy = MakeBattlerWith((Endurance, 0));
            var context = new BattleContext(player, enemy, timeDelta: 0, new Mulberry32(0));
            context.SwapActiveAndTargetBattlers();

            context.DamageTarget(20, EDamageType.Physical);

            Assert.Equal(0, context.Stats.AttacksDodged);
            Assert.Equal(0, context.Stats.DamageDodged, 0.001);
            Assert.Equal(0, context.Stats.AttacksBlocked);
            Assert.Equal(0, context.Stats.DamageBlocked, 0.001);
        }

        // ── DamageTarget: RNG draw order ─────────────────────────────────────

        [Fact]
        public void DamageTarget_DrawsOncePerPlayerHit_TwicePerEnemyHit()
        {
            // The draw count is a pure function of the fire sequence: one crit draw when the player attacks,
            // then two (dodge, block) when the enemy attacks. Verified by comparing the shared stream's
            // position against a reference advanced by exactly three draws — independent of the seed.
            const uint seed = 12345u;
            var rng = new Mulberry32(seed);
            var player = MakeBattlerWith((Endurance, 0));
            var enemy = MakeBattlerWith((Endurance, 0));
            var context = new BattleContext(player, enemy, timeDelta: 0, rng);

            context.DamageTarget(5, EDamageType.Physical);               // player attacking → 1 draw
            context.SwapActiveAndTargetBattlers();
            context.DamageTarget(5, EDamageType.Physical);               // enemy attacking → 2 draws

            var reference = new Mulberry32(seed);
            reference.Next();
            reference.Next();
            reference.Next();
            Assert.Equal(reference.Next(), rng.Next());
        }

        // ── DamageTarget: damage typing (amplification / resistance, #1320) ──

        [Fact]
        public void DamageTarget_AmplificationAndResistance_ApplyAsSeparateMultipliers()
        {
            // The attacker's amplification and the defender's resistance are SEPARATE budgets — × (1 + amp)
            // then × (1 − res) — not a single (1 + amp − res). With FireAmp 0.5 and FireResist 0.5 on a 40-damage
            // Fire hit: 40 × 1.5 = 60, × 0.5 = 30, − 2 Defense = 28. A combined budget would cancel to 40 − 2 = 38.
            var player = MakeBattlerWith((FireAmplification, 0.5));
            var enemy = MakeBattlerWith((Endurance, 0), (FireResistance, 0.5)); // Defense 2, MaxHealth 50
            var context = new BattleContext(player, enemy, timeDelta: 0, new Mulberry32(0));

            context.DamageTarget(40, EDamageType.Fire);

            Assert.Equal(50 - 28, enemy.CurrentHealth, 0.001);
            Assert.Equal(28, context.Stats.PlayerDamageDealt, 0.001);
        }

        [Fact]
        public void DamageTarget_FireAmplification_AlsoSummedFromElementalAmplification()
        {
            // applies(Fire) = { Fire, Elemental }, so a hit is amplified by the additive SUM of both keys.
            // FireAmp 0.3 + ElementalAmp 0.2 = 0.5 → 40 × 1.5 = 60, − 2 Defense = 58.
            var player = MakeBattlerWith((FireAmplification, 0.3), (ElementalAmplification, 0.2));
            var enemy = MakeBattlerWith((Endurance, 0));
            var context = new BattleContext(player, enemy, timeDelta: 0, new Mulberry32(0));

            context.DamageTarget(40, EDamageType.Fire);

            Assert.Equal(50 - 58, enemy.CurrentHealth, 0.001);
        }

        [Fact]
        public void DamageTarget_ResistanceAboveOne_HealsTheTargetAndIgnoresFlatDefense()
        {
            // Absorption: FireResistance 2.0 drives the post-resistance hit negative (20 × (1 − 2) = −20), a net
            // heal — and flat Defense is NOT subtracted from an absorbed hit (a heal of 20, not 18). A physical
            // hit first brings the enemy below MaxHealth so the heal lands (the heal is capped at MaxHealth).
            var player = MakeBattlerWith((Endurance, 0));
            var enemy = MakeBattlerWith((Endurance, 0), (FireResistance, 2.0)); // Defense 2, MaxHealth 50
            var context = new BattleContext(player, enemy, timeDelta: 0, new Mulberry32(0));
            context.DamageTarget(27, EDamageType.Physical); // 27 − 2 Defense = 25 → CurrentHealth 25

            context.DamageTarget(20, EDamageType.Fire);

            // Healed 20 (flat ignored) → 45; the booked damage is 25 (physical) + (−20) (absorption) = 5.
            Assert.Equal(45, enemy.CurrentHealth, 0.001);
            Assert.Equal(5, context.Stats.PlayerDamageDealt, 0.001);
        }

        [Fact]
        public void DamageTarget_CritAmplifiesBeforeResistanceAndDefense()
        {
            // Order: amp (none) → crit (× 2) → resist (× 0.5) → flat Defense. A normal hit (20 × 0.5 = 10, − 10
            // Defense) clamps to 0, but the crit (20 × 2 = 40, × 0.5 = 20, − 10 = 10) punches through.
            var player = MakeBattlerWith((CriticalChance, 1), (CriticalDamage, 0.5)); // CriticalDamage 2.0
            var enemy = MakeBattlerWith((Endurance, 0), (Defense, 8), (FireResistance, 0.5)); // Defense 10, MaxHealth 50
            var context = new BattleContext(player, enemy, timeDelta: 0, new Mulberry32(0));

            context.DamageTarget(20, EDamageType.Fire);

            Assert.Equal(50 - 10, enemy.CurrentHealth, 0.001);
        }

        // ── Helpers ──────────────────────────────────────────────────────────

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
