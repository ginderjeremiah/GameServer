using Game.Core.Attributes;
using Game.Core.Attributes.Modifiers;
using Game.Core.Battle;
using Game.Core.Skills;
using Xunit;

namespace Game.Core.Tests.Battle
{
    /// <summary>
    /// Direct unit tests for the combat arithmetic on <see cref="Battler"/>.
    /// <para>
    /// These mirror the frontend suite <c>UI/src/tests/lib/battle/battler.test.ts</c>:
    /// the <see cref="Battler.TakeDamage"/> defense-floor/death cases and the
    /// <see cref="Battler.GetCooldownMultiplier"/> formula are asserted here with the
    /// <b>same scenarios and the same expected results</b> as the frontend, so a future
    /// divergence in the defense/cooldown math fails on both sides (the same parity
    /// discipline used for <see cref="Mulberry32ParityTests"/> ⇄ <c>mulberry32-parity.test.ts</c>).
    /// Frontend-only concerns (skill-slot filling, render cooldowns, name/level wiring) are
    /// not part of the backend <see cref="Battler"/> and are intentionally not mirrored.
    /// </para>
    /// </summary>
    public class BattlerTests
    {
        // ── Construction (mirrors the frontend "reset" cases that apply here) ──

        [Fact]
        public void Constructor_CalculatesCurrentHealthFromDerivedMaxHealth()
        {
            // MaxHealth = base(50) + Endurance(20)*20 + Strength(10)*5 = 50 + 400 + 50 = 500.
            var battler = MakeBattler((EAttribute.Strength, 10), (EAttribute.Endurance, 20));

            Assert.Equal(500, battler.CurrentHealth);
        }

        [Fact]
        public void Constructor_StartsNotDead()
        {
            var battler = MakeBattler((EAttribute.Endurance, 10));

            Assert.False(battler.IsDead);
        }

        // ── GetCooldownMultiplier ─────────────────────────────────────────────

        [Fact]
        public void GetCooldownMultiplier_CalculatedFromCooldownRecovery()
        {
            // CooldownRecovery = 1 (base) + 0.004·Agility(20) + 0.001·Dexterity(10) = 1.09, read directly
            // as the cooldown multiplier (no 1 + x/100 transform).
            var battler = MakeBattler((EAttribute.Agility, 20), (EAttribute.Dexterity, 10));

            var expected = 1 + 0.004 * 20 + 0.001 * 10;
            Assert.Equal(expected, battler.GetCooldownMultiplier(), 10);
        }

        // ── TakeDamage ────────────────────────────────────────────────────────

        [Fact]
        public void TakeDamage_ReducesHealthByDamageMinusDefense()
        {
            // Defense = base(2) + Endurance(10)*1.0 = 12. MaxHealth = base(50) + Endurance(10)*20 = 250.
            var battler = MakeBattler((EAttribute.Endurance, 10));
            var initialHealth = battler.CurrentHealth;
            const double rawDamage = 50;

            var finalDamage = battler.TakeDamage(rawDamage);

            Assert.Equal(rawDamage - 12, finalDamage);
            Assert.Equal(initialHealth - finalDamage, battler.CurrentHealth);
        }

        [Fact]
        public void TakeDamage_FloorsDamageAtZero_WhenDefenseExceedsRawDamage()
        {
            // Defense = base(2) + Endurance(100)*1.0 = 102, which exceeds the raw damage of 5.
            var battler = MakeBattler((EAttribute.Endurance, 100));
            var initialHealth = battler.CurrentHealth;

            var finalDamage = battler.TakeDamage(5);

            Assert.Equal(0, finalDamage);
            Assert.Equal(initialHealth, battler.CurrentHealth);
        }

        [Fact]
        public void TakeDamage_SetsIsDead_WhenHealthDropsToZero()
        {
            // No attribute modifiers: Defense = base(2), MaxHealth = base(50).
            var battler = MakeBattler();
            var lethalRawDamage = battler.CurrentHealth + 2;

            battler.TakeDamage(lethalRawDamage);

            Assert.Equal(0, battler.CurrentHealth);
            Assert.True(battler.IsDead);
        }

        [Fact]
        public void TakeDamage_SetsIsDead_WhenHealthDropsBelowZero()
        {
            var battler = MakeBattler();

            battler.TakeDamage(99999);

            Assert.True(battler.CurrentHealth < 0);
            Assert.True(battler.IsDead);
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        /// <summary>
        /// Builds a <see cref="Battler"/> from a set of additive attribute amounts (sourced as
        /// <see cref="EAttributeModifierSource.AttributeDistribution"/>, matching how the frontend
        /// <c>BattleAttributes</c> feeds raw battler attributes) with no skills at level 1.
        /// </summary>
        private static Battler MakeBattler(params (EAttribute Attribute, double Amount)[] attributes)
        {
            var modifiers = attributes
                .Select(a => new AttributeModifier
                {
                    Attribute = a.Attribute,
                    Amount = a.Amount,
                    Type = EModifierType.Additive,
                    Source = EAttributeModifierSource.AttributeDistribution,
                })
                .ToList();

            return new Battler(new AttributeCollection(modifiers), [], 1);
        }
    }
}
