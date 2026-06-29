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

            var finalDamage = battler.TakeDamage(rawDamage, EDamageType.Physical);

            Assert.Equal(rawDamage - 12, finalDamage);
            Assert.Equal(initialHealth - finalDamage, battler.CurrentHealth);
        }

        [Fact]
        public void TakeDamage_FloorsDamageAtZero_WhenDefenseExceedsRawDamage()
        {
            // Defense = base(2) + Endurance(100)*1.0 = 102, which exceeds the raw damage of 5.
            var battler = MakeBattler((EAttribute.Endurance, 100));
            var initialHealth = battler.CurrentHealth;

            var finalDamage = battler.TakeDamage(5, EDamageType.Physical);

            Assert.Equal(0, finalDamage);
            Assert.Equal(initialHealth, battler.CurrentHealth);
        }

        [Fact]
        public void TakeDamage_SetsIsDead_WhenHealthDropsToZero()
        {
            // No attribute modifiers: Defense = base(2), MaxHealth = base(50).
            var battler = MakeBattler();
            var lethalRawDamage = battler.CurrentHealth + 2;

            battler.TakeDamage(lethalRawDamage, EDamageType.Physical);

            Assert.Equal(0, battler.CurrentHealth);
            Assert.True(battler.IsDead);
        }

        [Fact]
        public void TakeDamage_SetsIsDead_WhenHealthDropsBelowZero()
        {
            var battler = MakeBattler();

            battler.TakeDamage(99999, EDamageType.Physical);

            Assert.True(battler.CurrentHealth < 0);
            Assert.True(battler.IsDead);
        }

        // ── AmplifyDamage (attacker-side, #1320) ──────────────────────────────

        [Fact]
        public void AmplifyDamage_WithNoAmplification_LeavesDamageUnchanged()
        {
            // The reduce-to-today identity: with no amplification authored the factor is an exact 1.0.
            var battler = MakeBattler();

            Assert.Equal(20, battler.AmplifyDamage(20, EDamageType.Physical));
        }

        [Fact]
        public void AmplifyDamage_SumsTheTypesApplicableAmplificationKeys()
        {
            // applies(Fire) = { Fire, Elemental }, so amplification is the additive sum: 0.3 + 0.2 = 0.5 →
            // 40 × 1.5 = 60.
            var battler = MakeBattler(
                (EAttribute.FireAmplification, 0.3), (EAttribute.ElementalAmplification, 0.2));

            Assert.Equal(60, battler.AmplifyDamage(40, EDamageType.Fire), 0.001);
        }

        [Fact]
        public void AmplifyDamage_PhysicalIgnoresElementalAmplification()
        {
            // applies(Physical) = { Physical } only — Physical is not a cross-cutting category, so Elemental
            // amplification does not touch a physical hit.
            var battler = MakeBattler(
                (EAttribute.PhysicalAmplification, 0.5), (EAttribute.ElementalAmplification, 1.0));

            Assert.Equal(30, battler.AmplifyDamage(20, EDamageType.Physical), 0.001);
        }

        // ── TakeDamage: percentage resistance then flat Defense (#1320) ────────

        [Fact]
        public void TakeDamage_AppliesPercentageResistanceBeforeFlatDefense()
        {
            // FireResistance 0.5 halves the 40-damage hit to 20, then flat Defense 12 → 8 net.
            var battler = MakeBattler((EAttribute.Endurance, 10), (EAttribute.FireResistance, 0.5)); // Defense 12

            var net = battler.TakeDamage(40, EDamageType.Fire);

            Assert.Equal(8, net, 0.001);
        }

        [Fact]
        public void TakeDamage_SumsResistanceAcrossApplicableKeys()
        {
            // applies(Fire) = { Fire, Elemental }: 0.25 + 0.25 = 0.5 resistance → 40 × 0.5 = 20, − 2 Defense = 18.
            var battler = MakeBattler((EAttribute.FireResistance, 0.25), (EAttribute.ElementalResistance, 0.25));

            var net = battler.TakeDamage(40, EDamageType.Fire);

            Assert.Equal(18, net, 0.001);
        }

        [Fact]
        public void TakeDamage_NegativeResistance_AmplifiesAsVulnerability()
        {
            // Resistance is unclamped: −0.5 FireResistance makes the target take 1.5× (20 × 1.5 = 30, − 2 = 28).
            var battler = MakeBattler((EAttribute.FireResistance, -0.5)); // Defense 2

            var net = battler.TakeDamage(20, EDamageType.Fire);

            Assert.Equal(28, net, 0.001);
        }

        [Fact]
        public void TakeDamage_ResistanceAboveOne_HealsAndNeverAppliesFlatDefense()
        {
            // FireResistance 2.0 → 20 × (1 − 2) = −20: a net heal, with flat Defense NOT subtracted (a heal of
            // 20, not 22). Bring the battler below MaxHealth first so the whole heal lands.
            var battler = MakeBattler((EAttribute.Endurance, 0), (EAttribute.FireResistance, 2.0)); // Defense 2, MaxHealth 50
            battler.TakeDamage(27, EDamageType.Physical); // 27 − 2 Defense = 25 damage → CurrentHealth 25

            var net = battler.TakeDamage(20, EDamageType.Fire);

            Assert.Equal(-20, net, 0.001);
            Assert.Equal(45, battler.CurrentHealth, 0.001);
        }

        [Fact]
        public void TakeDamage_AbsorptionHeal_IsCappedAtMaxHealth()
        {
            // The absorption heal never overheals past MaxHealth (consistent with ApplyHealOverTime). Only 5 of
            // room remains, so a −20 absorption restores 5 and the net reported is the capped −5, not −20.
            var battler = MakeBattler((EAttribute.Endurance, 0), (EAttribute.FireResistance, 2.0)); // Defense 2, MaxHealth 50
            battler.TakeDamage(7, EDamageType.Physical); // 7 − 2 Defense = 5 damage → CurrentHealth 45

            var net = battler.TakeDamage(20, EDamageType.Fire);

            Assert.Equal(-5, net, 0.001);
            Assert.Equal(50, battler.CurrentHealth, 0.001);
        }

        [Fact]
        public void TakeDamage_TypedWithNoResistance_IsIdenticalToTheOldFlatStep()
        {
            // The reduce-to-today identity at the unit level: a typed hit with zero resistance is byte-for-byte
            // the old flat-Defense subtraction (50 − 12 = 38).
            var battler = MakeBattler((EAttribute.Endurance, 10)); // Defense 12

            Assert.Equal(38, battler.TakeDamage(50, EDamageType.Fire), 0.001);
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
