using Game.Core.Attributes.Modifiers;
using Game.Core.Battle;
using Game.Core.Players;
using Game.Core.Players.Inventories;
using Game.Core.Skills;

namespace Game.Core.Tests.Battle
{
    [TestClass]
    public class BattleSkillTests
    {
        // ── Initial state ────────────────────────────────────────────────────

        [TestMethod]
        public void NewBattleSkill_ChargeTimeIsZero()
        {
            var skill = MakeSkill(cooldownMs: 1000, baseDamage: 10);
            var battleSkill = new BattleSkill(skill);

            Assert.AreEqual(0.0, battleSkill.ChargeTime);
        }

        [TestMethod]
        public void NewBattleSkill_ExposesUnderlyingSkill()
        {
            var skill = MakeSkill(cooldownMs: 500, baseDamage: 20);
            var battleSkill = new BattleSkill(skill);

            Assert.AreEqual(skill, battleSkill.Skill);
        }

        // ── Update ───────────────────────────────────────────────────────────

        [TestMethod]
        public void Update_AccumulatesChargeTime_BeforeCooldown()
        {
            // Use a huge cooldown so the skill never fires during this test.
            var skill = MakeSkill(cooldownMs: 999_999, baseDamage: 0);
            var battleSkill = new BattleSkill(skill);
            var context = MakeContext(timeDelta: 100);

            battleSkill.Update(context);

            // CooldownMultiplier with no CooldownRecovery = 1.0, so ChargeTime ≈ 100.
            Assert.IsTrue(battleSkill.ChargeTime > 0, "ChargeTime should have increased.");
            Assert.IsTrue(battleSkill.ChargeTime < skill.CooldownMs, "ChargeTime should not have fired yet.");
        }

        [TestMethod]
        public void Update_WhenChargeTimeReachesCooldown_ResetsChargeTime()
        {
            var skill = MakeSkill(cooldownMs: 100, baseDamage: 0);
            var battleSkill = new BattleSkill(skill);
            var context = MakeContext(timeDelta: 200); // large enough to fire

            battleSkill.Update(context);

            Assert.AreEqual(0.0, battleSkill.ChargeTime, "ChargeTime should reset after firing.");
        }

        // ── CalculateDamage ──────────────────────────────────────────────────

        [TestMethod]
        public void CalculateDamage_NoMultipliers_ReturnsBaseDamage()
        {
            var skill = MakeSkill(cooldownMs: 1000, baseDamage: 50, multipliers: []);
            var battleSkill = new BattleSkill(skill);
            var context = MakeContext(timeDelta: 0);

            var damage = battleSkill.CalculateDamage(context);

            Assert.AreEqual(50.0, damage);
        }

        [TestMethod]
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
            Assert.AreEqual(25.0, damage, 0.001);
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
                StatPoints = new PlayerStatPoints(statAllocations)
                    { StatPointsGained = 50, StatPointsUsed = 50 },
                Inventory = new Inventory(),
                SelectedSkills = [],
                Skills = [],
            };
            return new Battler(player);
        }
    }
}
