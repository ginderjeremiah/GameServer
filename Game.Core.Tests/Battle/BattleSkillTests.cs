using Game.Core.Attributes.Modifiers;
using Game.Core.Battle;
using Game.Core.Players;
using Game.Core.Players.Inventories;
using Game.Core.Skills;
using Xunit;

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
            var multipliers = new List<AttributeModifier>
            {
                new() { Attribute = EAttribute.Strength, Amount = 2.0, Type = EModifierType.Additive, Source = EAttributeModifierSource.Item }
            };
            var skill = MakeSkill(cooldownMs: 1000, baseDamage: 5, multipliers: multipliers);
            var battleSkill = new BattleSkill(skill);

            var attacker = MakeBattler(strength: 10);
            var defender = MakeBattler(strength: 0);
            var context = new BattleContext(attacker, defender, timeDelta: 0);

            var damage = battleSkill.CalculateDamage(context);

            // Strength=10, multiplier=2.0 → bonus = 20; BaseDamage=5 → total = 25
            Assert.Equal(25.0, damage, 0.001);
        }

        [Fact]
        public void CalculateDamage_OnHotPath_DoesNotAllocate()
        {
            // CalculateDamage runs on every skill fire, the hottest sub-path of the simulation, so it
            // must stay allocation-free (the prior LINQ `.Sum(lambda)` boxed a list enumerator and
            // captured a closure on every call — #286). Measure managed allocations on this thread
            // across a batch of calls and assert none, locking the optimization in against regression.
            var multipliers = new List<AttributeModifier>
            {
                new() { Attribute = EAttribute.Strength,  Amount = 2.0, Type = EModifierType.Additive, Source = EAttributeModifierSource.Item },
                new() { Attribute = EAttribute.Endurance, Amount = 1.5, Type = EModifierType.Additive, Source = EAttributeModifierSource.Item },
                new() { Attribute = EAttribute.Agility,   Amount = 0.5, Type = EModifierType.Additive, Source = EAttributeModifierSource.Item },
            };
            var skill = MakeSkill(cooldownMs: 1000, baseDamage: 5, multipliers: multipliers);
            var battleSkill = new BattleSkill(skill);

            var attacker = MakeBattler(strength: 10);
            var defender = MakeBattler(strength: 0);
            var context = new BattleContext(attacker, defender, timeDelta: 0);

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

        // ── Helpers ──────────────────────────────────────────────────────────

        private static Skill MakeSkill(int cooldownMs, double baseDamage, List<AttributeModifier>? multipliers = null) => new()
        {
            Id = 1,
            Name = "Test Skill",
            Description = string.Empty,
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
            return new BattleContext(attacker, defender, timeDelta);
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
            var player = new Player
            {
                Id = 0,
                Name = "t",
                Level = 1,
                Exp = 0,
                CurrentZoneId = 0,
                StatPoints = new PlayerStatPoints
                { StatAllocations = statAllocations, StatPointsGained = 50, StatPointsUsed = 50 },
                Inventory = new Inventory(),
                SelectedSkills = [],
                Skills = [],
                LogPreferences = [],
            };
            return new Battler(player);
        }
    }
}
