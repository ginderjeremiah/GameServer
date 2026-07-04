using Game.Core.Attributes;
using Game.Core.Attributes.Modifiers;
using Game.Core.Skills;
using static Game.Core.EAttribute;

namespace Game.Core.Battle
{
    /// <summary>
    /// The combat-rating classification of an <see cref="EAttribute"/> — the maintenance contract from spike
    /// #1526 Decision 9: every attribute is assigned one via <see cref="CombatRating.Classify"/>, backed by an
    /// exhaustiveness test, so a newly added attribute fails the build until its rating term is decided rather
    /// than being silently unpriced.
    /// </summary>
    public enum ECombatRatingClassification
    {
        /// <summary>Feeds <see cref="CombatRating"/>'s offense rate (amplification, ExecuteBonus, …).</summary>
        Offense,

        /// <summary>Feeds <see cref="CombatRating"/>'s survivability (MaxHealth, resistances, Toughness, regen, …).</summary>
        Survivability,

        /// <summary>Feeds both sides (e.g. a core attribute that derives into both an offense and a defense term).</summary>
        Both,

        /// <summary>Deliberately unpriced — inert by design (obsolete) or read structurally rather than as a scalar term.</summary>
        NeutralByDesign,

        /// <summary>Priced only for a player battler; skipped entirely for an enemy (crit/dodge/parry).</summary>
        AsymmetryGated,
    }

    /// <summary>
    /// The closed-form capability rating decided in spike #1526
    /// (<c>docs/spikes/1526-combat-rating-power-measure.md</c>): <c>Rate = √(OffenseRate × Survivability)</c>,
    /// computed from an assembled <see cref="Battler"/> against fixed reference profiles
    /// (<see cref="ServerGameConstants"/>). The geometric mean is exact, not aesthetic — for any matchup,
    /// <c>(enemyRating / playerRating)²</c> <b>is</b> the time-to-kill ratio in this engine.
    /// <para>
    /// Server-only: this is never simulated client-side (display values are sent, not recomputed), so it
    /// carries no frontend/backend parity surface. Reuses the exact <see cref="Battler"/>/<see cref="BattleSkill"/>
    /// methods the engine itself uses (<see cref="BattleSkill.CalculateRawDamage"/>, <see cref="Battler.AmplifyDamage"/>,
    /// <see cref="Battler.ComputeNetDamage"/>, <see cref="Battler.GetCooldownMultiplier"/>) — the anti-drift
    /// mechanism that keeps the rating from silently diverging from the real damage pipeline.
    /// </para>
    /// <para>
    /// Enemies do not crit, dodge, or parry in this engine, so those terms are skipped entirely for a non-player
    /// battler (the <paramref name="isPlayer"/>-gated terms below) even when an enemy skill carries an authored
    /// <c>CriticalChance</c> — otherwise the rating would recreate the dead-LUK defect one level up (pricing a
    /// capability that can never fire).
    /// </para>
    /// </summary>
    public static class CombatRating
    {
        // Degenerate-guard floor: keeps offense/survivability strictly positive so a zero-offense or
        // zero-survivability battler never divides a downstream consumer (e.g. a rating ratio) by zero.
        private const double Epsilon = 1e-6;

        /// <summary>
        /// The rating for <paramref name="battler"/> as assembled — <c>√(OffenseRate × Survivability)</c>
        /// against the fixed reference profiles in <see cref="ServerGameConstants"/>. Pass
        /// <paramref name="isPlayer"/> <c>true</c> for a player battler (enables the crit/dodge/parry/riposte
        /// terms) and <c>false</c> for an enemy (the engine's own asymmetry).
        /// </summary>
        public static double Rate(Battler battler, bool isPlayer)
        {
            var effectiveCaster = BuildEffectiveCaster(battler);
            var referenceDefense = BuildReferenceDefense(battler);

            var offense = Math.Max(OffenseRate(battler, isPlayer, effectiveCaster, referenceDefense), Epsilon);
            var survivability = Math.Max(Survivability(isPlayer, effectiveCaster), Epsilon);

            return Math.Sqrt(offense * survivability);
        }

        /// <summary>
        /// The finite-difference marginal contribution of <paramref name="delta"/> more points of
        /// <paramref name="attribute"/> to <paramref name="battler"/>'s rating — <c>Rate(bumped) − Rate(battler)</c>.
        /// Doubles as the dead-stat detector the enemy content lint (#1529) needs: an attribute whose fielded
        /// kit has no matching enabler (e.g. AGI with no dodge/cadence source) marginals to exactly <c>0</c>.
        /// </summary>
        public static double Marginal(Battler battler, bool isPlayer, EAttribute attribute, double delta = 1.0)
        {
            var baseline = Rate(battler, isPlayer);
            var bumped = battler.CloneWithAttributeDelta(attribute, delta);
            return Rate(bumped, isPlayer) - baseline;
        }

        /// <summary>
        /// The combat-rating classification for every <see cref="EAttribute"/> — the exhaustiveness contract
        /// (spike #1526 Decision 9). Throws for an unclassified member, so a newly added attribute fails the
        /// build (via the backing test enumerating the whole enum) until its rating term is decided here.
        /// </summary>
        public static ECombatRatingClassification Classify(EAttribute attribute)
        {
#pragma warning disable CS0618 // DropBonus is obsolete but still a seeded, classifiable enum member.
            return attribute switch
            {
                Strength => ECombatRatingClassification.Both,
                Endurance => ECombatRatingClassification.Survivability,
                Intellect => ECombatRatingClassification.Offense,
                Agility => ECombatRatingClassification.Both,
                Dexterity => ECombatRatingClassification.Offense,
                Luck => ECombatRatingClassification.AsymmetryGated,
                MaxHealth => ECombatRatingClassification.Survivability,
                Toughness => ECombatRatingClassification.Survivability,
                CooldownRecovery => ECombatRatingClassification.Offense,
                DropBonus => ECombatRatingClassification.NeutralByDesign,
                CriticalChanceMultiplier => ECombatRatingClassification.AsymmetryGated,
                CriticalDamage => ECombatRatingClassification.AsymmetryGated,
                DodgeChance => ECombatRatingClassification.AsymmetryGated,
                BleedDamagePerSecond => ECombatRatingClassification.NeutralByDesign,
                HealthRegenPerSecond => ECombatRatingClassification.Survivability,
                PhysicalAmplification => ECombatRatingClassification.Offense,
                PhysicalResistance => ECombatRatingClassification.Survivability,
                FireAmplification => ECombatRatingClassification.Offense,
                FireResistance => ECombatRatingClassification.Survivability,
                WaterAmplification => ECombatRatingClassification.Offense,
                WaterResistance => ECombatRatingClassification.Survivability,
                EarthAmplification => ECombatRatingClassification.Offense,
                EarthResistance => ECombatRatingClassification.Survivability,
                WindAmplification => ECombatRatingClassification.Offense,
                WindResistance => ECombatRatingClassification.Survivability,
                BleedAmplification => ECombatRatingClassification.Offense,
                BleedResistance => ECombatRatingClassification.Survivability,
                PoisonAmplification => ECombatRatingClassification.Offense,
                PoisonResistance => ECombatRatingClassification.Survivability,
                BurnAmplification => ECombatRatingClassification.Offense,
                BurnResistance => ECombatRatingClassification.Survivability,
                ElementalAmplification => ECombatRatingClassification.Offense,
                ElementalResistance => ECombatRatingClassification.Survivability,
                DotAmplification => ECombatRatingClassification.Offense,
                DotResistance => ECombatRatingClassification.Survivability,
                PoisonDamagePerSecond => ECombatRatingClassification.NeutralByDesign,
                BurnDamagePerSecond => ECombatRatingClassification.NeutralByDesign,
                SwordAmplification => ECombatRatingClassification.Offense,
                AxeAmplification => ECombatRatingClassification.Offense,
                BowAmplification => ECombatRatingClassification.Offense,
                ClubAmplification => ECombatRatingClassification.Offense,
                DaggerAmplification => ECombatRatingClassification.Offense,
                UnarmedAmplification => ECombatRatingClassification.Offense,
                DamageReflection => ECombatRatingClassification.Offense,
                ExecuteBonus => ECombatRatingClassification.Offense,
                ParryChance => ECombatRatingClassification.AsymmetryGated,
                ParryChanceMultiplier => ECombatRatingClassification.AsymmetryGated,
                DodgeChanceMultiplier => ECombatRatingClassification.AsymmetryGated,
                // The cadence pair (#1524/#1526): both feed the offense rate through GetCooldownMultiplier
                // (faster cycling = more DPS), like CooldownRecovery — the committed CooldownBonus enabler and the
                // Agility-derived multiplier that scales it. Symmetric (enemies cycle too), so not asymmetry-gated.
                CooldownBonus => ECombatRatingClassification.Offense,
                CooldownBonusMultiplier => ECombatRatingClassification.Offense,
                _ => throw new ArgumentOutOfRangeException(
                    nameof(attribute), attribute, "No combat-rating classification defined for the given attribute."),
            };
#pragma warning restore CS0618
        }

        // ── Offense ──────────────────────────────────────────────────────────

        private static double OffenseRate(Battler battler, bool isPlayer, Battler effectiveCaster, Battler referenceDefense)
        {
            var total = 0.0;

            foreach (var battleSkill in battler.Skills)
            {
                var skill = battleSkill.Skill;
                var effectiveCooldownSec = skill.CooldownMs / effectiveCaster.GetCooldownMultiplier() / 1000.0;
                if (effectiveCooldownSec <= 0)
                {
                    // Degenerate guard: unreachable through authored content (CooldownRecovery's static base
                    // alone is already 1), but a debuffed-to-zero-or-below multiplier must not divide by zero.
                    continue;
                }

                total += ExpectedDirectHit(skill, isPlayer, effectiveCaster, referenceDefense) / effectiveCooldownSec;

                foreach (var effect in skill.Effects)
                {
                    if (effect.Target != ESkillEffectTarget.Opponent)
                    {
                        continue;
                    }

                    if (DamageTypes.DotTypeForAccumulator(effect.AttributeId) is not EDamageType dotType)
                    {
                        continue;
                    }

                    // The exact DoT steady-state closed form (spike #1526 Decision 1): magnitude × frozen
                    // amplification × duration ÷ effective cooldown. DoT bypasses the reference Toughness
                    // (its real property), so no mitigation term applies here. The scaling attribute is read
                    // off the original battler, not effectiveCaster — mirroring FoldedEffectMagnitude's "the
                    // caster's ORIGINAL attribute" rule, so the two DoT-adjacent reads agree.
                    var magnitude = effect.Amount + battler.GetAttributeValue(effect.ScalingAttributeId) * effect.ScalingAmount;
                    var ampedMagnitude = effectiveCaster.AmplifyDamage(magnitude, dotType);
                    var durationSec = effect.DurationMs / 1000.0;
                    total += ampedMagnitude * durationSec / effectiveCooldownSec;
                }
            }

            // Reflect: DamageReflection × the reference incoming DPS.
            total += effectiveCaster.GetAttributeValue(DamageReflection) * ServerGameConstants.RefDps;

            // Riposte (player-side only): effectiveParryChance × reference attack rate × the counter's expected hit.
            if (isPlayer && battler.CounterSkill is Skill counterSkill)
            {
                var effectiveParryChance = effectiveCaster.GetAttributeValue(ParryChance) * effectiveCaster.GetAttributeValue(ParryChanceMultiplier);
                total += effectiveParryChance * ServerGameConstants.RefAttackRate
                    * ExpectedDirectHit(counterSkill, isPlayer, effectiveCaster, referenceDefense);
            }

            return total;
        }

        // The expected post-mitigation damage of one fire of <paramref name="skill"/>, amplified by
        // <paramref name="effectiveCaster"/> and mitigated by <paramref name="referenceDefense"/> — shared by
        // a skill's own direct hit and the parry counter's phantom fire (both are "one hit of a skill").
        private static double ExpectedDirectHit(Skill skill, bool isPlayer, Battler effectiveCaster, Battler referenceDefense)
        {
            var raw = BattleSkill.CalculateRawDamage(skill, effectiveCaster);
            var portions = skill.DamagePortions;
            var totalWeight = 0.0;
            foreach (var portion in portions)
            {
                totalWeight += portion.Weight;
            }

            var afterMitigation = 0.0;
            foreach (var portion in portions)
            {
                var rawPortion = raw * portion.Weight / totalWeight;
                var amped = effectiveCaster.AmplifyDamage(rawPortion, portion.Type);
                afterMitigation += referenceDefense.ComputeNetDamage(amped, portion.Type);
            }

            // Enemies never crit in this engine — gating this to isPlayer mirrors the same asymmetry the live
            // damage pipeline enforces (BattleContext.DamageTarget), so an authored enemy CriticalChance is
            // never priced as a capability that cannot fire.
            var critExpectation = isPlayer
                ? 1.0 + skill.CriticalChance * effectiveCaster.GetAttributeValue(CriticalChanceMultiplier)
                      * (effectiveCaster.GetAttributeValue(CriticalDamage) - 1.0)
                : 1.0;
            var executeExpectation = 1.0 + effectiveCaster.GetAttributeValue(ExecuteBonus) * 0.5;

            return afterMitigation * critExpectation * executeExpectation;
        }

        // ── Survivability ────────────────────────────────────────────────────

        private static double Survivability(bool isPlayer, Battler effectiveCaster)
        {
            var maxHealth = effectiveCaster.GetAttributeValue(MaxHealth);
            var regenRate = effectiveCaster.GetAttributeValue(HealthRegenPerSecond);
            var effectiveHealth = maxHealth + regenRate * ServerGameConstants.RefFightDuration;

            // Avoid (parry-then-dodge) is player-only — enemies never parry or dodge in this engine.
            var avoid = 0.0;
            if (isPlayer)
            {
                var parry = effectiveCaster.GetAttributeValue(ParryChance) * effectiveCaster.GetAttributeValue(ParryChanceMultiplier);
                var dodge = effectiveCaster.GetAttributeValue(DodgeChance) * effectiveCaster.GetAttributeValue(DodgeChanceMultiplier);
                avoid = parry + (1 - parry) * dodge;
            }

            var resist = AverageIncomingResistance(effectiveCaster);

            // The Toughness curve, inlined rather than through Battler.ComputeNetDamage: survivability needs
            // the dimensionless mitigation fraction, not a transformed damage amount, but the tunable knob
            // (GameConstants.ToughnessMitigationConstant) is still the single shared constant.
            var toughness = effectiveCaster.GetAttributeValue(Toughness);
            var toughnessReduction = toughness / (toughness + GameConstants.ToughnessMitigationConstant);

            var mitigationFraction = (1 - avoid) * (1 - resist) * (1 - toughnessReduction);
            return effectiveHealth / Math.Max(mitigationFraction, Epsilon);
        }

        // The uniform average, across ServerGameConstants.RefIncomingTypeMix, of each type's own summed
        // resistance (a type may sum more than one key, e.g. Fire = FireResistance + ElementalResistance).
        private static double AverageIncomingResistance(Battler effectiveCaster)
        {
            var mix = ServerGameConstants.RefIncomingTypeMix;
            var total = 0.0;
            foreach (var type in mix)
            {
                foreach (var attribute in DamageTypes.ResistanceAttributes(type))
                {
                    total += effectiveCaster.GetAttributeValue(attribute);
                }
            }

            return total / mix.Count;
        }

        // ── Steady-state effect pricing (spike #1526 Decision 2) ────────────

        // A battler equal to the assembled one but with every fielded Self-targeted effect folded in at its
        // uptime-weighted average magnitude — so offense/survivability compute once from one order-free state
        // rather than simulating the battle. Every other attribute is copied at its exact composed value,
        // INCLUDING the core attributes: BattleSkill.CalculateRawDamage sums DamageMultipliers over core
        // attributes directly (not through a derived static), so a core-stripped snapshot would zero out every
        // core-scaled skill's damage. Seeding the real cores lets a core-derived attribute (CooldownRecovery,
        // Toughness, MaxHealth, the crit/parry/dodge multipliers) re-derive naturally from them via the fresh
        // AttributeCollection's automatic re-application of StaticAttributeModifiers — so each non-core
        // attribute's explicit delta below is computed against a "cores-only" baseline (the value the real
        // cores alone would produce) rather than an all-zero one, crediting only the extra (gear/proficiency/
        // effect) contribution on top and avoiding double-counting the core-derived share.
        private static Battler BuildEffectiveCaster(Battler battler)
        {
            var coreModifiers = Attributes.Attribute.CoreAttributes
                .Select(a => new AttributeModifier
                {
                    Attribute = a,
                    Amount = battler.GetAttributeValue(a),
                    Type = EModifierType.Additive,
                    Source = EAttributeModifierSource.BaseValue,
                })
                .ToList();
            var coreOnlyBaseline = new AttributeCollection(coreModifiers);

            var modifiers = new List<AttributeModifier>(coreModifiers);
            foreach (var attribute in Enum.GetValues<EAttribute>())
            {
                if (Attributes.Attribute.IsCore(attribute))
                {
                    continue; // already seeded directly above
                }

                modifiers.Add(new AttributeModifier
                {
                    Attribute = attribute,
                    Amount = battler.GetAttributeValue(attribute) - coreOnlyBaseline[attribute],
                    Type = EModifierType.Additive,
                    Source = EAttributeModifierSource.BaseValue,
                });
            }

            foreach (var battleSkill in battler.Skills)
            {
                var skill = battleSkill.Skill;
                foreach (var effect in skill.Effects)
                {
                    if (effect.Target != ESkillEffectTarget.Self)
                    {
                        continue;
                    }

                    var (type, amount) = FoldedEffectMagnitude(battler, skill, effect);
                    modifiers.Add(new AttributeModifier
                    {
                        Attribute = effect.AttributeId,
                        Amount = amount,
                        Type = type,
                        Source = EAttributeModifierSource.SkillEffect,
                    });
                }
            }

            return new Battler(new AttributeCollection(modifiers), [], battler.Level);
        }

        // A fresh reference-defense profile (Toughness = RefToughness, everything else at its static default)
        // with every fielded Opponent-targeted, non-DoT-accumulator effect (a Hex resistance debuff, a Sunder
        // Toughness debuff, …) folded in at its uptime-weighted average magnitude — so a debuff-authored skill
        // is priced as raising the offense rate against a softened reference target. Opponent-targeted DoT
        // effects are excluded here; they are priced as their own exact closed-form term in OffenseRate instead.
        private static Battler BuildReferenceDefense(Battler battler)
        {
            var modifiers = new List<AttributeModifier>
            {
                new()
                {
                    Attribute = Toughness,
                    Amount = ServerGameConstants.RefToughness,
                    Type = EModifierType.Additive,
                    Source = EAttributeModifierSource.BaseValue,
                },
            };

            foreach (var battleSkill in battler.Skills)
            {
                var skill = battleSkill.Skill;
                foreach (var effect in skill.Effects)
                {
                    if (effect.Target != ESkillEffectTarget.Opponent)
                    {
                        continue;
                    }

                    if (DamageTypes.DotTypeForAccumulator(effect.AttributeId) is not null)
                    {
                        continue;
                    }

                    var (type, amount) = FoldedEffectMagnitude(battler, skill, effect);
                    modifiers.Add(new AttributeModifier
                    {
                        Attribute = effect.AttributeId,
                        Amount = amount,
                        Type = type,
                        Source = EAttributeModifierSource.SkillEffect,
                    });
                }
            }

            return new Battler(new AttributeCollection(modifiers), [], battler.Level);
        }

        // The uptime-weighted average magnitude of one fielded effect (spike #1526 Decision 2):
        // (Amount + casterAttribute × ScalingAmount) × min(DurationMs, RefFightDuration/2) ÷ effectiveCooldownMs
        // — one closed form covering both the non-ramp uptime case (d ≤ cd → d/cd) and the shared-expiry ramp
        // case (d > cd), whose average alive magnitude over a reference fight is RefFightDuration/(2·cd).
        // Scaling always reads the caster's ORIGINAL (unfolded) attribute — mirroring how the live engine
        // always scales an effect off the caster at apply time — so folding stays order-free.
        private static (EModifierType Type, double Amount) FoldedEffectMagnitude(Battler battler, Skill skill, SkillEffect effect)
        {
            var effectiveCooldownMs = skill.CooldownMs / battler.GetCooldownMultiplier();
            var cappedDurationMs = Math.Min(effect.DurationMs, ServerGameConstants.RefFightDuration * 1000.0 / 2.0);
            var uptime = effectiveCooldownMs > 0 ? cappedDurationMs / effectiveCooldownMs : 0.0;
            var rawMagnitude = effect.Amount + battler.GetAttributeValue(effect.ScalingAttributeId) * effect.ScalingAmount;

            if (effect.ModifierType == EModifierType.Multiplicative)
            {
                // A multiplicative factor is centered on 1 (a no-op); interpolate toward neutral by uptime
                // rather than scaling the factor itself, so a partial-uptime buff is a partial buff rather than
                // a nonsensical sub-1 multiply.
                return (EModifierType.Multiplicative, 1.0 + (rawMagnitude - 1.0) * uptime);
            }

            return (EModifierType.Additive, rawMagnitude * uptime);
        }
    }
}
