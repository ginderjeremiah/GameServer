using Game.Core.Attributes;
using Game.Core.Attributes.Modifiers;
using Game.Core.Battle;
using Game.Core.Classes;
using Game.Core.Skills;
using Xunit;

namespace Game.Core.Tests.Battle
{
    /// <summary>
    /// Direct unit tests for the combat arithmetic on <see cref="Battler"/>.
    /// <para>
    /// These mirror the frontend suite <c>UI/src/tests/lib/battle/battler.test.ts</c>:
    /// the <see cref="Battler.TakeDamage"/> toughness-curve/death cases, the
    /// <see cref="Battler.GetCooldownMultiplier"/> formula, and the
    /// <see cref="Battler.EffectiveParryChance"/>/<see cref="Battler.EffectiveDodgeChance"/>
    /// avoidance products, and the <see cref="Battler.EffectiveCriticalChance"/>/
    /// <see cref="Battler.ExecuteInvestment"/> offense compositions are asserted here with the
    /// <b>same scenarios and the same expected results</b> as the frontend, so a future
    /// divergence in the mitigation/cadence/avoidance/offense math fails on both sides (the same parity
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

        // ── GetCooldownMultiplier: CooldownRecovery + CooldownBonus × CooldownBonusMultiplier (#1426) ──
        // Mirrors the frontend battler.test.ts cdMultiplier cases with the same scenarios and results.

        [Fact]
        public void GetCooldownMultiplier_NoEnabler_ChargesAtBaseRateRegardlessOfAgility()
        {
            // CDR is severed from the core attributes (#1426): with no authored CooldownBonus, Agility only lifts
            // the (idle) CooldownBonusMultiplier, so the effective rate is exactly the base-1 CooldownRecovery.
            var battler = MakeBattler((EAttribute.Agility, 50), (EAttribute.Dexterity, 20));

            Assert.Equal(1.0, battler.GetCooldownMultiplier(), 10);
        }

        [Fact]
        public void GetCooldownMultiplier_AuthoredBonus_ScalesByAgilityAmplifiedMultiplier()
        {
            // CooldownBonus 0.5 (authored enabler) × CooldownBonusMultiplier (1 + 0.002·Agility(20) = 1.04), added
            // to the base-1 CooldownRecovery → 1 + 0.5·1.04 = 1.52.
            var battler = MakeBattler((EAttribute.CooldownBonus, 0.5), (EAttribute.Agility, 20));

            var expected = 1.0 + 0.5 * (1 + 0.002 * 20);
            Assert.Equal(expected, battler.GetCooldownMultiplier(), 10);
        }

        [Fact]
        public void GetCooldownMultiplier_AuthoredBonus_NoAgility_UsesBaseMultiplier()
        {
            // With no Agility the multiplier stays at its base 1, so the bonus adds verbatim: 1 + 0.5·1 = 1.5.
            var battler = MakeBattler((EAttribute.CooldownBonus, 0.5));

            Assert.Equal(1.5, battler.GetCooldownMultiplier(), 10);
        }

        // ── EffectiveParryChance / EffectiveDodgeChance: enabler × multiplier (#2429) ──
        // The products the engine's own draws read (BattleContext.DamageTarget) and the combat rating prices.
        // Mirrors the frontend battler.test.ts cases with the same scenarios and results.

        [Fact]
        public void EffectiveParryChance_NoEnabler_IsZeroRegardlessOfLuck()
        {
            // ParryChance is an authored-only enabler idling at 0, so Luck only lifts the (idle) multiplier:
            // 0 × 1.1 = 0. An un-enabled build never parries.
            var battler = MakeBattler((EAttribute.Luck, 50));

            Assert.Equal(0.0, battler.EffectiveParryChance, 10);
        }

        [Fact]
        public void EffectiveParryChance_AuthoredChance_ScalesByLuckAmplifiedMultiplier()
        {
            // ParryChance 0.2 (authored enabler) × ParryChanceMultiplier (1 + 0.002·Luck(20) = 1.04) = 0.208.
            var battler = MakeBattler((EAttribute.ParryChance, 0.2), (EAttribute.Luck, 20));

            Assert.Equal(0.2 * (1 + 0.002 * 20), battler.EffectiveParryChance, 10);
        }

        [Fact]
        public void EffectiveParryChance_AuthoredChance_NoLuck_UsesBaseMultiplier()
        {
            // With no Luck the multiplier stays at its base 1, so the enabler passes through verbatim.
            var battler = MakeBattler((EAttribute.ParryChance, 0.2));

            Assert.Equal(0.2, battler.EffectiveParryChance, 10);
        }

        [Fact]
        public void EffectiveParryChance_ProductAboveOne_IsNotClamped()
        {
            // Deliberately unclamped: the engine draws against [0, 1), so a product at or above 1 saturates
            // naturally. Clamping is CombatRating's concern (it prices an expectation, not a draw), and folding
            // it in here would change engine behavior for no reason. 0.8 × (1 + 0.002·500 = 2) = 1.6.
            var battler = MakeBattler((EAttribute.ParryChance, 0.8), (EAttribute.Luck, 500));

            Assert.Equal(1.6, battler.EffectiveParryChance, 10);
        }

        [Fact]
        public void EffectiveParryChance_IsReadLive_ReflectingAMidBattleMultiplierChange()
        {
            // Read at consumption (per draw), not frozen at assembly, so a timed effect on either factor takes
            // effect on the next fire — the same live-read contract as GetCooldownMultiplier.
            var battler = MakeBattler((EAttribute.ParryChance, 0.2));
            Assert.Equal(0.2, battler.EffectiveParryChance, 10);

            battler.ApplyEffect(ParryMultiplierBuff, ParryMultiplierBuff.Amount);

            Assert.Equal(0.4, battler.EffectiveParryChance, 10);
        }

        [Fact]
        public void EffectiveDodgeChance_NoEnabler_IsZeroRegardlessOfAgility()
        {
            // DodgeChance shares ParryChance's authored-only, base-0 enabler shape, so Agility alone dodges
            // nothing: 0 × 1.1 = 0.
            var battler = MakeBattler((EAttribute.Agility, 50));

            Assert.Equal(0.0, battler.EffectiveDodgeChance, 10);
        }

        [Fact]
        public void EffectiveDodgeChance_AuthoredChance_ScalesByAgilityAmplifiedMultiplier()
        {
            // DodgeChance 0.25 × DodgeChanceMultiplier (1 + 0.002·Agility(20) = 1.04) = 0.26.
            var battler = MakeBattler((EAttribute.DodgeChance, 0.25), (EAttribute.Agility, 20));

            Assert.Equal(0.25 * (1 + 0.002 * 20), battler.EffectiveDodgeChance, 10);
        }

        [Fact]
        public void EffectiveDodgeChance_AuthoredChance_NoAgility_UsesBaseMultiplier()
        {
            var battler = MakeBattler((EAttribute.DodgeChance, 0.25));

            Assert.Equal(0.25, battler.EffectiveDodgeChance, 10);
        }

        // ── EffectiveCriticalChance: per-skill enabler × multiplier (#2439) ──
        // The product the engine's own crit draw reads (BattleContext.DamageTarget's player-fire half) and the
        // combat rating prices. Unlike the avoidance pair the enabler is a per-skill authored value (#1453), so
        // it is passed in. Mirrors the frontend battler.test.ts cases with the same scenarios and results.

        [Fact]
        public void EffectiveCriticalChance_UnauthoredSkill_IsZeroRegardlessOfLuck()
        {
            // A skill's CriticalChance defaults to 0 (authored-only opt-in), so Luck only lifts the (idle)
            // multiplier: 0 × 1.1 = 0. An un-authored skill never crits.
            var battler = MakeBattler((EAttribute.Luck, 50));

            Assert.Equal(0.0, battler.EffectiveCriticalChance(0.0), 10);
        }

        [Fact]
        public void EffectiveCriticalChance_AuthoredSkill_ScalesByLuckAmplifiedMultiplier()
        {
            // Skill CriticalChance 0.2 × CriticalChanceMultiplier (1 + 0.002·Luck(20) = 1.04) = 0.208.
            var battler = MakeBattler((EAttribute.Luck, 20));

            Assert.Equal(0.2 * (1 + 0.002 * 20), battler.EffectiveCriticalChance(0.2), 10);
        }

        [Fact]
        public void EffectiveCriticalChance_AuthoredSkill_NoLuck_UsesBaseMultiplier()
        {
            // With no Luck the multiplier stays at its base 1, so the skill's authored chance passes through.
            var battler = MakeBattler();

            Assert.Equal(0.2, battler.EffectiveCriticalChance(0.2), 10);
        }

        [Fact]
        public void EffectiveCriticalChance_ProductAboveOne_IsNotClamped()
        {
            // Deliberately unclamped for EffectiveParryChance's reason: the engine draws against [0, 1), so a
            // product at or above 1 saturates naturally, and clamping is CombatRating's concern (it prices an
            // expectation, not a draw). 0.8 × (1 + 0.002·500 = 2) = 1.6.
            var battler = MakeBattler((EAttribute.Luck, 500));

            Assert.Equal(1.6, battler.EffectiveCriticalChance(0.8), 10);
        }

        [Fact]
        public void EffectiveCriticalChance_IsReadLive_ReflectingAMidBattleMultiplierChange()
        {
            // Read at consumption (per fire), not frozen at assembly, so a timed effect on the multiplier takes
            // effect on the next fire — the same live-read contract as GetCooldownMultiplier.
            var battler = MakeBattler();
            Assert.Equal(0.2, battler.EffectiveCriticalChance(0.2), 10);

            battler.ApplyEffect(CriticalChanceMultiplierBuff, CriticalChanceMultiplierBuff.Amount);

            Assert.Equal(0.4, battler.EffectiveCriticalChance(0.2), 10);
        }

        // ── ExecuteInvestment: ExecuteBonus × the target's missing-health fraction (#1430/#2439) ──
        // The composition the engine's damage multiplier and Cull overlay tally both read, and the one the
        // combat rating prices against its reference fraction. Mirrors the frontend battler.test.ts cases.

        [Fact]
        public void ExecuteInvestment_NoEnabler_IsZeroAtAnyMissingHealth()
        {
            // ExecuteBonus is authored-only and idles at 0, so an un-enabled build invests nothing even against
            // a nearly-dead target — its multiplier stays an exact 1.
            var battler = MakeBattler((EAttribute.Strength, 50));

            Assert.Equal(0.0, battler.ExecuteInvestment(0.9), 10);
        }

        [Fact]
        public void ExecuteInvestment_AuthoredBonus_ScalesByMissingHealthFraction()
        {
            // ExecuteBonus 0.5 × missing-health fraction 0.75 = 0.375 (a 1.375× damage multiplier).
            var battler = MakeBattler((EAttribute.ExecuteBonus, 0.5));

            Assert.Equal(0.375, battler.ExecuteInvestment(0.75), 10);
        }

        [Fact]
        public void ExecuteInvestment_FullHealthTarget_IsZero()
        {
            // At a full-health target the fraction is 0, so an authored bonus invests nothing this fire.
            var battler = MakeBattler((EAttribute.ExecuteBonus, 0.5));

            Assert.Equal(0.0, battler.ExecuteInvestment(0.0), 10);
        }

        // ── TakeDamage: Toughness mitigation curve ─────────────────────────────

        [Fact]
        public void TakeDamage_AppliesToughnessMitigationCurve()
        {
            // Toughness = 2·Endurance(25) = 50. The curve reduces by 50 / (50 + 200) = 0.2, so a 50 hit
            // deals 40. MaxHealth = base(50) + Endurance(25)*20 = 550.
            var battler = MakeBattler((EAttribute.Endurance, 25));
            var initialHealth = battler.CurrentHealth;

            var finalDamage = battler.TakeDamage(50, EDamageType.Physical);

            Assert.Equal(40, finalDamage, 0.001);
            Assert.Equal(initialHealth - 40, battler.CurrentHealth, 0.001);
        }

        [Fact]
        public void TakeDamage_ToughnessAtTheConstant_MitigatesExactlyHalf()
        {
            // The constant is the curve's half-point (#1487): Toughness = 2·Endurance(100) = 200 = C reduces
            // by exactly 200 / (200 + 200) = 0.5, so a 50 hit deals 25 — the per-item legibility anchor.
            var battler = MakeBattler((EAttribute.Endurance, 100));

            Assert.Equal(25, battler.TakeDamage(50, EDamageType.Physical), 0.001);
        }

        [Fact]
        public void TakeDamage_NoToughness_LeavesHitUnreduced()
        {
            // The reduce-to-nothing identity: with no Endurance, Toughness is 0, so the curve's reduction is
            // 0 / (0 + C) = 0 and the hit lands in full.
            var battler = MakeBattler();

            Assert.Equal(40, battler.TakeDamage(40, EDamageType.Physical), 0.001);
        }

        [Fact]
        public void TakeDamage_ToughnessNeverFullyMitigates()
        {
            // The curve asymptotes below 100% (no immunity, no breakpoint): even overwhelming Toughness
            // (Endurance 1000 → Toughness 2000) leaves a positive sliver,
            // 5 × 200 / (2000 + 200) = 0.4545…, never zero.
            var battler = MakeBattler((EAttribute.Endurance, 1000));

            var net = battler.TakeDamage(5, EDamageType.Physical);

            Assert.True(net > 0);
            Assert.Equal(5.0 * 200 / 2200, net, 0.001);
        }

        [Fact]
        public void TakeDamage_EffectiveHpIsLinearInToughness()
        {
            // EHP linearity: the effective-HP multiplier (raw / net = (Toughness + C) / C) rises by a
            // CONSTANT amount per point of Toughness, even though the % reduction itself diminishes.
            // Toughness 0 → ×1 EHP, 200 → ×2, 400 → ×3 — equal +1 steps per 200 Toughness, so each point
            // is worth the same slice of EHP and is never useless.
            var ehp0 = 40 / MakeBattler().TakeDamage(40, EDamageType.Physical);
            var ehp200 = 40 / MakeBattler((EAttribute.Endurance, 100)).TakeDamage(40, EDamageType.Physical);
            var ehp400 = 40 / MakeBattler((EAttribute.Endurance, 200)).TakeDamage(40, EDamageType.Physical);

            Assert.Equal(1.0, ehp0, 0.001);
            Assert.Equal(2.0, ehp200, 0.001);
            Assert.Equal(3.0, ehp400, 0.001);
            Assert.Equal(ehp200 - ehp0, ehp400 - ehp200, 0.001); // equal steps ⇒ linear in Toughness
        }

        [Fact]
        public void TakeDamage_SetsIsDead_WhenHealthDropsToZero()
        {
            // No attribute modifiers: Toughness = 0 (no reduction), MaxHealth = base(50).
            var battler = MakeBattler();
            var lethalRawDamage = battler.CurrentHealth;

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

        // ── TakeDamage: percentage resistance then the Toughness curve (#1320 / #1330) ────

        [Fact]
        public void TakeDamage_AppliesPercentageResistanceBeforeToughness()
        {
            // FireResistance 0.5 halves the 40-damage hit to 20, then the Toughness curve (Toughness 200 =
            // the half-point → 0.5 reduction) halves that to 10 net.
            var battler = MakeBattler((EAttribute.Endurance, 100), (EAttribute.FireResistance, 0.5)); // Toughness 200

            var net = battler.TakeDamage(40, EDamageType.Fire);

            Assert.Equal(10, net, 0.001);
        }

        [Fact]
        public void TakeDamage_SumsResistanceAcrossApplicableKeys()
        {
            // applies(Fire) = { Fire, Elemental }: 0.25 + 0.25 = 0.5 resistance → 40 × 0.5 = 20. No Endurance, so
            // Toughness is 0 and the curve leaves it unchanged: 20 net.
            var battler = MakeBattler((EAttribute.FireResistance, 0.25), (EAttribute.ElementalResistance, 0.25));

            var net = battler.TakeDamage(40, EDamageType.Fire);

            Assert.Equal(20, net, 0.001);
        }

        [Fact]
        public void TakeDamage_NegativeResistance_AmplifiesAsVulnerability()
        {
            // Resistance is unclamped: −0.5 FireResistance makes the target take 1.5× (20 × 1.5 = 30). Toughness
            // 0 (no Endurance) leaves the curve a no-op, so the full 30 lands.
            var battler = MakeBattler((EAttribute.FireResistance, -0.5)); // Toughness 0

            var net = battler.TakeDamage(20, EDamageType.Fire);

            Assert.Equal(30, net, 0.001);
        }

        [Fact]
        public void TakeDamage_ResistanceAboveOne_HealsAndNeverAppliesMitigation()
        {
            // FireResistance 2.0 → 20 × (1 − 2) = −20: a net heal, with the Toughness curve NOT applied (it can
            // neither heal nor deepen an absorption). Bring the battler below MaxHealth first so the whole heal lands.
            var battler = MakeBattler((EAttribute.Endurance, 0), (EAttribute.FireResistance, 2.0)); // Toughness 0, MaxHealth 50
            battler.TakeDamage(30, EDamageType.Physical); // 30 damage (no Toughness) → CurrentHealth 20

            var net = battler.TakeDamage(20, EDamageType.Fire);

            Assert.Equal(-20, net, 0.001);
            Assert.Equal(40, battler.CurrentHealth, 0.001);
        }

        [Fact]
        public void TakeDamage_AbsorptionHeal_IsCappedAtMaxHealth()
        {
            // The absorption heal never overheals past MaxHealth (consistent with ApplyHealOverTime). Only 5 of
            // room remains, so a −20 absorption restores 5 and the net reported is the capped −5, not −20.
            var battler = MakeBattler((EAttribute.Endurance, 0), (EAttribute.FireResistance, 2.0)); // Toughness 0, MaxHealth 50
            battler.TakeDamage(5, EDamageType.Physical); // 5 damage (no Toughness) → CurrentHealth 45

            var net = battler.TakeDamage(20, EDamageType.Fire);

            Assert.Equal(-5, net, 0.001);
            Assert.Equal(50, battler.CurrentHealth, 0.001);
        }

        [Fact]
        public void TakeDamage_TypedWithResistanceThenToughness_ComposesBothSteps()
        {
            // Resistance and the Toughness curve are separate multiplicative steps. FireResistance 0.5 halves a
            // 50 hit to 25, then Toughness 200 (the half-point → 0.5) halves it to 12.5.
            var battler = MakeBattler((EAttribute.Endurance, 100), (EAttribute.FireResistance, 0.5)); // Toughness 200

            Assert.Equal(12.5, battler.TakeDamage(50, EDamageType.Fire), 0.001);
        }

        [Fact]
        public void TakeDamage_NegativeToughnessWithinThePole_AmplifiesTheHit()
        {
            // A debuff-driven negative Toughness (2·Endurance(-50) = -100) inverts the curve rather than
            // flooring at 0% mitigation (#1478): -100/(-100+200) = -1 reduction → 40 × (1 − (−1)) = 80.
            var battler = MakeBattler((EAttribute.Endurance, -50));

            Assert.Equal(80, battler.TakeDamage(40, EDamageType.Physical), 0.001);
        }

        // ── CloneWithAttributeDelta: signature-passive re-derivation (#1862) ────

        [Fact]
        public void CloneWithAttributeDelta_AttributeScaledSignaturePassive_ReDerivesAgainstBumpedAttribute()
        {
            // The passive scales off Endurance (+0.5 Toughness per point) on top of the static 2×Endurance
            // derivation. Bumping Endurance by 10 must re-derive the passive's own contribution too, not copy
            // through its pre-bump amount — the exact defect #1862 reports.
            var passive = new ClassSignaturePassive
            {
                Attribute = EAttribute.Toughness,
                Amount = 0m,
                ScalingAttribute = EAttribute.Endurance,
                ScalingAmount = 0.5m,
                ModifierType = EModifierType.Additive,
            };
            var battler = MakeBattlerWithSignaturePassive(passive, (EAttribute.Endurance, 20));
            Assert.Equal(50, battler.GetAttributeValue(EAttribute.Toughness), 0.001); // 2×20 + 0.5×20

            var bumped = battler.CloneWithAttributeDelta(EAttribute.Endurance, 10);

            // Endurance 30: static 2×30 = 60, passive re-derived at 0.5×30 = 15 → 75. A frozen-passive bug would
            // instead yield 60 + 10 (the pre-bump amount) = 70.
            Assert.Equal(75, bumped.GetAttributeValue(EAttribute.Toughness), 0.001);
        }

        [Fact]
        public void CloneWithAttributeDelta_FlatSignaturePassive_CarriesThroughUnaffectedByAnUnrelatedBump()
        {
            // A flat (non-scaled) passive has nothing to re-derive; bumping an unrelated attribute must still
            // leave its contribution exactly as assembled, with no duplication from the exclude/re-add swap.
            var passive = new ClassSignaturePassive
            {
                Attribute = EAttribute.Strength,
                Amount = 7m,
                ScalingAttribute = null,
                ScalingAmount = 0m,
                ModifierType = EModifierType.Additive,
            };
            var battler = MakeBattlerWithSignaturePassive(passive, (EAttribute.Endurance, 5));
            Assert.Equal(7, battler.GetAttributeValue(EAttribute.Strength), 0.001);

            var bumped = battler.CloneWithAttributeDelta(EAttribute.Endurance, 10);

            Assert.Equal(7, bumped.GetAttributeValue(EAttribute.Strength), 0.001);
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        /// <summary>
        /// A +1 additive <see cref="EAttribute.ParryChanceMultiplier"/> buff — lands on the base 1, doubling
        /// the composed parry chance.
        /// </summary>
        private static readonly SkillEffect ParryMultiplierBuff = new()
        {
            Id = 1,
            Target = ESkillEffectTarget.Self,
            AttributeId = EAttribute.ParryChanceMultiplier,
            ModifierType = EModifierType.Additive,
            Amount = 1.0,
            DurationMs = 1000,
            ScalingAttributeId = EAttribute.Luck,
            ScalingAmount = 0,
        };

        /// <summary>
        /// A +1 additive <see cref="EAttribute.CriticalChanceMultiplier"/> buff — lands on the base 1, doubling
        /// the composed critical-hit chance of whatever the firing skill authored.
        /// </summary>
        private static readonly SkillEffect CriticalChanceMultiplierBuff = new()
        {
            Id = 2,
            Target = ESkillEffectTarget.Self,
            AttributeId = EAttribute.CriticalChanceMultiplier,
            ModifierType = EModifierType.Additive,
            Amount = 1.0,
            DurationMs = 1000,
            ScalingAttributeId = EAttribute.Luck,
            ScalingAmount = 0,
        };

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

        /// <summary>
        /// Builds a <see cref="Battler"/> like <see cref="MakeBattler"/>, plus a resolved
        /// <see cref="EAttributeModifierSource.Class"/> signature-passive modifier composed last (mirroring
        /// <see cref="BattlerMaterials.Build"/>) and carried through for <see cref="Battler.CloneWithAttributeDelta"/>
        /// to re-resolve.
        /// </summary>
        private static Battler MakeBattlerWithSignaturePassive(
            ClassSignaturePassive passive, params (EAttribute Attribute, double Amount)[] attributes)
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

            var collection = new AttributeCollection(modifiers);
            collection.AddModifier(passive.GetModifier(collection.GetAttributeValue));

            return new Battler(collection, [], 1, signaturePassive: passive);
        }
    }
}
