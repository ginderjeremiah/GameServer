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
    /// the <see cref="Battler.TakeDamage"/> toughness-curve/death cases and the
    /// <see cref="Battler.GetCooldownMultiplier"/> formula are asserted here with the
    /// <b>same scenarios and the same expected results</b> as the frontend, so a future
    /// divergence in the mitigation/cooldown math fails on both sides (the same parity
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

        // ── TakeDamage: Toughness mitigation curve ─────────────────────────────

        [Fact]
        public void TakeDamage_AppliesToughnessMitigationCurve()
        {
            // Toughness = 2·Endurance(10) = 20. Against a level-1 attacker the curve reduces by
            // 20 / (20 + 20·1) = 0.5, so a 50 hit deals 25. MaxHealth = base(50) + Endurance(10)*20 = 250.
            var battler = MakeBattler((EAttribute.Endurance, 10));
            var initialHealth = battler.CurrentHealth;

            var finalDamage = battler.TakeDamage(50, EDamageType.Physical, attackerLevel: 1);

            Assert.Equal(25, finalDamage, 0.001);
            Assert.Equal(initialHealth - 25, battler.CurrentHealth, 0.001);
        }

        [Fact]
        public void TakeDamage_NoToughness_LeavesHitUnreduced()
        {
            // The reduce-to-nothing identity: with no Endurance, Toughness is 0, so the curve's reduction is
            // 0 / (0 + K·level) = 0 and the hit lands in full.
            var battler = MakeBattler();

            Assert.Equal(40, battler.TakeDamage(40, EDamageType.Physical, attackerLevel: 1), 0.001);
        }

        [Fact]
        public void TakeDamage_ToughnessNeverFullyMitigates()
        {
            // The curve asymptotes below 100% (no immunity, no breakpoint): even overwhelming Toughness
            // (Endurance 100 → Toughness 200) against a level-1 attacker leaves a positive sliver,
            // 5 × 20 / (200 + 20) = 0.4545…, never zero.
            var battler = MakeBattler((EAttribute.Endurance, 100));

            var net = battler.TakeDamage(5, EDamageType.Physical, attackerLevel: 1);

            Assert.True(net > 0);
            Assert.Equal(5.0 * 20 / 220, net, 0.001);
        }

        [Fact]
        public void TakeDamage_AttackerLevelScalesTheCurve()
        {
            // K·attackerLevel scales the denominator, so the same Toughness mitigates less against a
            // higher-level attacker. Toughness 20: vs level 1 → 20/(20+20)=0.5 (40→20); vs level 3 →
            // 20/(20+60)=0.25 (40→30).
            var lowLevel = MakeBattler((EAttribute.Endurance, 10));
            var highLevel = MakeBattler((EAttribute.Endurance, 10));

            Assert.Equal(20, lowLevel.TakeDamage(40, EDamageType.Physical, attackerLevel: 1), 0.001);
            Assert.Equal(30, highLevel.TakeDamage(40, EDamageType.Physical, attackerLevel: 3), 0.001);
        }

        [Fact]
        public void TakeDamage_EffectiveHpIsLinearInToughness()
        {
            // EHP linearity: the effective-HP multiplier (raw / net = (Toughness + K·L) / (K·L)) rises by a
            // CONSTANT amount per point of Toughness, even though the % reduction itself diminishes. Against a
            // level-1 attacker (K·L = 20): Toughness 0 → ×1 EHP, 20 → ×2, 40 → ×3 — equal +1 steps per 20
            // Toughness, so each point is worth the same slice of EHP and is never useless.
            var ehp0 = 40 / MakeBattler().TakeDamage(40, EDamageType.Physical, attackerLevel: 1);
            var ehp20 = 40 / MakeBattler((EAttribute.Endurance, 10)).TakeDamage(40, EDamageType.Physical, attackerLevel: 1);
            var ehp40 = 40 / MakeBattler((EAttribute.Endurance, 20)).TakeDamage(40, EDamageType.Physical, attackerLevel: 1);

            Assert.Equal(1.0, ehp0, 0.001);
            Assert.Equal(2.0, ehp20, 0.001);
            Assert.Equal(3.0, ehp40, 0.001);
            Assert.Equal(ehp20 - ehp0, ehp40 - ehp20, 0.001); // equal steps ⇒ linear in Toughness
        }

        [Fact]
        public void TakeDamage_SetsIsDead_WhenHealthDropsToZero()
        {
            // No attribute modifiers: Toughness = 0 (no reduction), MaxHealth = base(50).
            var battler = MakeBattler();
            var lethalRawDamage = battler.CurrentHealth;

            battler.TakeDamage(lethalRawDamage, EDamageType.Physical, attackerLevel: 1);

            Assert.Equal(0, battler.CurrentHealth);
            Assert.True(battler.IsDead);
        }

        [Fact]
        public void TakeDamage_SetsIsDead_WhenHealthDropsBelowZero()
        {
            var battler = MakeBattler();

            battler.TakeDamage(99999, EDamageType.Physical, attackerLevel: 1);

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

        // ── TakeDamage: percentage resistance then the Toughness curve (#1320 / #1330) ────

        [Fact]
        public void TakeDamage_AppliesPercentageResistanceBeforeToughness()
        {
            // FireResistance 0.5 halves the 40-damage hit to 20, then the Toughness curve (Toughness 20 vs a
            // level-1 attacker → 0.5 reduction) halves that to 10 net.
            var battler = MakeBattler((EAttribute.Endurance, 10), (EAttribute.FireResistance, 0.5)); // Toughness 20

            var net = battler.TakeDamage(40, EDamageType.Fire, attackerLevel: 1);

            Assert.Equal(10, net, 0.001);
        }

        [Fact]
        public void TakeDamage_SumsResistanceAcrossApplicableKeys()
        {
            // applies(Fire) = { Fire, Elemental }: 0.25 + 0.25 = 0.5 resistance → 40 × 0.5 = 20. No Endurance, so
            // Toughness is 0 and the curve leaves it unchanged: 20 net.
            var battler = MakeBattler((EAttribute.FireResistance, 0.25), (EAttribute.ElementalResistance, 0.25));

            var net = battler.TakeDamage(40, EDamageType.Fire, attackerLevel: 1);

            Assert.Equal(20, net, 0.001);
        }

        [Fact]
        public void TakeDamage_NegativeResistance_AmplifiesAsVulnerability()
        {
            // Resistance is unclamped: −0.5 FireResistance makes the target take 1.5× (20 × 1.5 = 30). Toughness
            // 0 (no Endurance) leaves the curve a no-op, so the full 30 lands.
            var battler = MakeBattler((EAttribute.FireResistance, -0.5)); // Toughness 0

            var net = battler.TakeDamage(20, EDamageType.Fire, attackerLevel: 1);

            Assert.Equal(30, net, 0.001);
        }

        [Fact]
        public void TakeDamage_ResistanceAboveOne_HealsAndNeverAppliesMitigation()
        {
            // FireResistance 2.0 → 20 × (1 − 2) = −20: a net heal, with the Toughness curve NOT applied (it can
            // neither heal nor deepen an absorption). Bring the battler below MaxHealth first so the whole heal lands.
            var battler = MakeBattler((EAttribute.Endurance, 0), (EAttribute.FireResistance, 2.0)); // Toughness 0, MaxHealth 50
            battler.TakeDamage(30, EDamageType.Physical, attackerLevel: 1); // 30 damage (no Toughness) → CurrentHealth 20

            var net = battler.TakeDamage(20, EDamageType.Fire, attackerLevel: 1);

            Assert.Equal(-20, net, 0.001);
            Assert.Equal(40, battler.CurrentHealth, 0.001);
        }

        [Fact]
        public void TakeDamage_AbsorptionHeal_IsCappedAtMaxHealth()
        {
            // The absorption heal never overheals past MaxHealth (consistent with ApplyHealOverTime). Only 5 of
            // room remains, so a −20 absorption restores 5 and the net reported is the capped −5, not −20.
            var battler = MakeBattler((EAttribute.Endurance, 0), (EAttribute.FireResistance, 2.0)); // Toughness 0, MaxHealth 50
            battler.TakeDamage(5, EDamageType.Physical, attackerLevel: 1); // 5 damage (no Toughness) → CurrentHealth 45

            var net = battler.TakeDamage(20, EDamageType.Fire, attackerLevel: 1);

            Assert.Equal(-5, net, 0.001);
            Assert.Equal(50, battler.CurrentHealth, 0.001);
        }

        [Fact]
        public void TakeDamage_TypedWithResistanceThenToughness_ComposesBothSteps()
        {
            // Resistance and the Toughness curve are separate multiplicative steps. FireResistance 0.5 halves a
            // 50 hit to 25, then Toughness 20 (vs a level-1 attacker → 0.5) halves it to 12.5.
            var battler = MakeBattler((EAttribute.Endurance, 10), (EAttribute.FireResistance, 0.5)); // Toughness 20

            Assert.Equal(12.5, battler.TakeDamage(50, EDamageType.Fire, attackerLevel: 1), 0.001);
        }

        // ── TakeDamage: Toughness curve is floored at 0 (#1461) ────────────────

        [Fact]
        public void TakeDamage_NegativeToughness_BehavesLikeZeroToughness()
        {
            // A debuff-driven negative Toughness (e.g. -10, well short of the pole at -20 for a level-1
            // attacker) floors to 0 for the curve, so the reduction is 0 and the full 40 lands unmitigated —
            // exactly like TakeDamage_NoToughness_LeavesHitUnreduced.
            var battler = MakeBattler((EAttribute.Toughness, -10));

            Assert.Equal(40, battler.TakeDamage(40, EDamageType.Physical, attackerLevel: 1), 0.001);
        }

        [Fact]
        public void TakeDamage_ToughnessAtThePole_DoesNotDivideByZero()
        {
            // Toughness = -K·attackerLevel (-20 vs a level-1 attacker) is exactly the curve's unfloored pole
            // (denominator 0). Floored at 0 it is a clean no-reduction case instead of a division by zero.
            var battler = MakeBattler((EAttribute.Toughness, -20));

            Assert.Equal(40, battler.TakeDamage(40, EDamageType.Physical, attackerLevel: 1), 0.001);
        }

        [Fact]
        public void TakeDamage_ToughnessPastThePole_NeverAmplifiesOrHeals()
        {
            // Past the unfloored pole (-20 vs a level-1 attacker) the raw curve would invert into
            // amplification/healing; floored at 0 the hit still lands unmitigated and never goes negative.
            var battler = MakeBattler((EAttribute.Toughness, -1000));

            var net = battler.TakeDamage(40, EDamageType.Physical, attackerLevel: 1);

            Assert.Equal(40, net, 0.001);
            Assert.True(net > 0);
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
