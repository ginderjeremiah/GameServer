using Game.Core.Attributes.Modifiers;
using Game.Core.Battle;
using Game.Core.Players;
using Game.Core.Skills;
using Game.Core.TestInfrastructure.Builders;
using Xunit;
using static Game.Core.EAttribute;

namespace Game.Core.Tests.Battle
{
    public class BattleSkillTests
    {
        // ── Initial state ────────────────────────────────────────────────────

        [Fact]
        public void NewBattleSkill_ChargeTimeIsZero()
        {
            var skill = MakeSkill(cooldownMs: 1000, baseDamage: 10);
            var battleSkill = new BattleSkill(skill);

            Assert.Equal(0.0, battleSkill.ChargeTime);
        }

        [Fact]
        public void NewBattleSkill_ExposesUnderlyingSkill()
        {
            var skill = MakeSkill(cooldownMs: 500, baseDamage: 20);
            var battleSkill = new BattleSkill(skill);

            Assert.Equal(skill, battleSkill.Skill);
        }

        // ── Update ───────────────────────────────────────────────────────────

        [Fact]
        public void Update_AccumulatesChargeTime_BeforeCooldown()
        {
            // Use a huge cooldown so the skill never fires during this test.
            var skill = MakeSkill(cooldownMs: 999_999, baseDamage: 0);
            var battleSkill = new BattleSkill(skill);
            var context = MakeContext(timeDelta: 100);

            battleSkill.Update(context);

            // CooldownMultiplier with no CooldownRecovery = 1.0, so ChargeTime ≈ 100.
            Assert.True(battleSkill.ChargeTime > 0, "ChargeTime should have increased.");
            Assert.True(battleSkill.ChargeTime < skill.CooldownMs, "ChargeTime should not have fired yet.");
        }

        [Fact]
        public void Update_WhenChargeTimeReachesCooldown_ResetsChargeTime()
        {
            var skill = MakeSkill(cooldownMs: 100, baseDamage: 0);
            var battleSkill = new BattleSkill(skill);
            var context = MakeContext(timeDelta: 200); // large enough to fire

            battleSkill.Update(context);

            Assert.Equal(0.0, battleSkill.ChargeTime);
        }

        // ── CalculateDamage ──────────────────────────────────────────────────

        [Fact]
        public void CalculateDamage_NoMultipliers_ReturnsBaseDamage()
        {
            var skill = MakeSkill(cooldownMs: 1000, baseDamage: 50, multipliers: []);
            var battleSkill = new BattleSkill(skill);
            var context = MakeContext(timeDelta: 0);

            var damage = battleSkill.CalculateDamage(context);

            Assert.Equal(50.0, damage);
        }

        [Fact]
        public void CalculateDamage_WithStrengthMultiplier_AddsBonusDamage()
        {
            // The active battler (attacker) has 10 Strength.
            // Multiplier: Strength × 2.0 bonus.
            // Expected: BaseDamage(5) + 10 * 2.0 = 25.
            var multipliers = new List<DamageMultiplier>
            {
                new() { Attribute = EAttribute.Strength, Amount = 2.0 }
            };
            var skill = MakeSkill(cooldownMs: 1000, baseDamage: 5, multipliers: multipliers);
            var battleSkill = new BattleSkill(skill);

            var attacker = MakeBattler(strength: 10);
            var defender = MakeBattler(strength: 0);
            var context = new BattleContext(attacker, defender, timeDelta: 0, new Mulberry32(0));

            var damage = battleSkill.CalculateDamage(context);

            // Strength=10, multiplier=2.0 → bonus = 20; BaseDamage=5 → total = 25
            Assert.Equal(25.0, damage, 0.001);
        }

        [Fact]
        public void CalculateDamage_FloatGrouping_MatchesBackend()
        {
            // Per-hit-damage parity guard (mirrors battle-formulas.test.ts "groups the multiplier sum like the
            // backend"). Floating-point addition is not associative, so `base + (m1 + m2)` and a naive
            // `(base + m1) + m2` can differ by a ULP at a kill boundary — a live-vs-replay desync (#802). Two
            // contributions each below baseDamage's ULP vanish if added one at a time but survive when summed
            // first, so the correct grouping lifts the result above baseDamage and the wrong one returns it
            // unchanged — pinning the exact ordering, not just a coarse outcome.
            const double tiny = 1e-16; // below the ULP of 1.0, so a single contribution is lost when added to base
            var multipliers = new List<DamageMultiplier>
            {
                new() { Attribute = EAttribute.Strength, Amount = tiny },
                new() { Attribute = EAttribute.Agility,  Amount = tiny },
            };
            var skill = MakeSkill(cooldownMs: 1000, baseDamage: 1, multipliers: multipliers);
            var battleSkill = new BattleSkill(skill);

            var statAllocations = new List<StatAllocation>
            {
                new() { Attribute = EAttribute.Strength,  Amount = 1 },
                new() { Attribute = EAttribute.Agility,   Amount = 1 },
                new() { Attribute = EAttribute.Endurance, Amount = 50 },
                new() { Attribute = EAttribute.Intellect, Amount = 0 },
                new() { Attribute = EAttribute.Dexterity, Amount = 0 },
                new() { Attribute = EAttribute.Luck,      Amount = 0 },
            };
            var attacker = BattlerFactory.FromPlayer(new PlayerBuilder()
                .WithStatAllocations(statAllocations)
                .WithStatPointsGained(52)
                .WithStatPointsUsed(52)
                .Build());
            var defender = MakeBattler(strength: 0);
            var context = new BattleContext(attacker, defender, timeDelta: 0, new Mulberry32(0));

            const double c1 = 1 * tiny;
            const double c2 = 1 * tiny;
            const double correct = 1 + (c1 + c2); // base + (m1 + m2)
            const double naive = 1 + c1 + c2;     // ((base + m1) + m2)
            // Sanity: the inputs are ULP-sensitive — the two groupings genuinely differ.
            Assert.NotEqual(correct, naive);
            Assert.Equal(1.0, naive);

            var damage = battleSkill.CalculateDamage(context);

            Assert.Equal(correct, damage);
            Assert.True(damage > skill.BaseDamage, "Correct grouping must lift the result above baseDamage.");
        }

        [Fact]
        public void CalculateDamage_OnHotPath_DoesNotAllocate()
        {
            // CalculateDamage runs on every skill fire, the hottest sub-path of the simulation, so it
            // must stay allocation-free (the prior LINQ `.Sum(lambda)` boxed a list enumerator and
            // captured a closure on every call — #286). Measure managed allocations on this thread
            // across a batch of calls and assert none, locking the optimization in against regression.
            var multipliers = new List<DamageMultiplier>
            {
                new() { Attribute = EAttribute.Strength,  Amount = 2.0 },
                new() { Attribute = EAttribute.Endurance, Amount = 1.5 },
                new() { Attribute = EAttribute.Agility,   Amount = 0.5 },
            };
            var skill = MakeSkill(cooldownMs: 1000, baseDamage: 5, multipliers: multipliers);
            var battleSkill = new BattleSkill(skill);

            var attacker = MakeBattler(strength: 10);
            var defender = MakeBattler(strength: 0);
            var context = new BattleContext(attacker, defender, timeDelta: 0, new Mulberry32(0));

            // Warm up so JIT compilation and the AttributeCollection's first-access value caching
            // happen before measuring — otherwise their one-off allocations would be counted.
            for (var i = 0; i < 200; i++)
            {
                _ = battleSkill.CalculateDamage(context);
            }

            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < 1000; i++)
            {
                _ = battleSkill.CalculateDamage(context);
            }
            var allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.Equal(0, allocatedBytes);
        }

        // ── Per-skill stats reconcile with global stats (#835) ───────────────

        [Fact]
        public void Update_PlayerCrit_RecordsActualDamagePerSkill_ReconcilingWithGlobal()
        {
            // A crit makes the raw pre-mitigation value understate the real hit: BaseDamage 20, crit
            // ×2 ⇒ 40, then −2 enemy Defense ⇒ 38 actual. The per-skill stat must book 38 (not the raw
            // 20) so it reconciles with the global stat DamageTarget books from the same hit.
            var skill = MakeSkill(cooldownMs: 100, baseDamage: 20);
            var battleSkill = new BattleSkill(skill);

            // CriticalChance 1 always crits; CriticalDamage base 1.5 + 0.5 = 2.0.
            var player = MakeBattlerWith((CriticalChance, 1), (CriticalDamage, 0.5));
            var enemy = MakeBattlerWith((Endurance, 0)); // MaxHealth 50, Defense 2
            var context = new BattleContext(player, enemy, timeDelta: 200, new Mulberry32(0));

            battleSkill.Update(context);

            var skillStats = context.Stats.SkillStats[skill.Id];
            Assert.Equal(38.0, skillStats.TotalDamage, 0.001);
            Assert.NotEqual(20.0, skillStats.TotalDamage, 0.001); // not the raw pre-crit/pre-defense value
            Assert.Equal(context.Stats.PlayerDamageDealt, skillStats.TotalDamage, 0.001);
            Assert.Equal(context.Stats.HighestPlayerAttack, skillStats.HighestSingleAttack, 0.001);
        }

        [Fact]
        public void Update_EnemyDefenseClamp_RecordsActualDamagePerSkill_ReconcilingWithGlobal()
        {
            // High enemy Defense makes the raw value overstate the real hit (the other direction of the
            // bug): BaseDamage 20 − 12 Defense ⇒ 8 actual. The per-skill stat must book 8, not 20.
            var skill = MakeSkill(cooldownMs: 100, baseDamage: 20);
            var battleSkill = new BattleSkill(skill);

            var player = MakeBattlerWith((CriticalChance, 0)); // never crits
            var enemy = MakeBattlerWith((Endurance, 10));      // Defense = 2 + 10 = 12, MaxHealth 250
            var context = new BattleContext(player, enemy, timeDelta: 200, new Mulberry32(0));

            battleSkill.Update(context);

            var skillStats = context.Stats.SkillStats[skill.Id];
            Assert.Equal(8.0, skillStats.TotalDamage, 0.001);
            Assert.NotEqual(20.0, skillStats.TotalDamage, 0.001); // not the raw pre-defense value
            Assert.Equal(context.Stats.PlayerDamageDealt, skillStats.TotalDamage, 0.001);
            Assert.Equal(context.Stats.HighestPlayerAttack, skillStats.HighestSingleAttack, 0.001);
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private static Skill MakeSkill(int cooldownMs, double baseDamage, List<DamageMultiplier>? multipliers = null) => new()
        {
            Id = 1,
            Name = "Test Skill",
            Description = string.Empty,
            Rarity = ERarity.Common,
            DamageType = EDamageType.Physical,
            CooldownMs = cooldownMs,
            BaseDamage = baseDamage,
            DamageMultipliers = multipliers ?? [],
            Effects = [],
        };

        /// <summary>
        /// Creates a BattleContext using two minimal battlers built from throwaway players.
        /// The active battler (attacker) is on the left.
        /// </summary>
        private static BattleContext MakeContext(int timeDelta)
        {
            var attacker = MakeBattler(strength: 0);
            var defender = MakeBattler(strength: 0);
            return new BattleContext(attacker, defender, timeDelta, new Mulberry32(0));
        }

        private static Battler MakeBattler(double strength = 0)
        {
            var statAllocations = new List<StatAllocation>
            {
                new() { Attribute = EAttribute.Strength,  Amount = strength },
                new() { Attribute = EAttribute.Endurance, Amount = 50 },   // gives MaxHealth
                new() { Attribute = EAttribute.Intellect, Amount = 0 },
                new() { Attribute = EAttribute.Agility,   Amount = 0 },
                new() { Attribute = EAttribute.Dexterity, Amount = 0 },
                new() { Attribute = EAttribute.Luck,      Amount = 0 },
            };
            var player = new PlayerBuilder()
                .WithStatAllocations(statAllocations)
                .WithStatPointsGained(50)
                .WithStatPointsUsed(50)
                .Build();
            return BattlerFactory.FromPlayer(player);
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
