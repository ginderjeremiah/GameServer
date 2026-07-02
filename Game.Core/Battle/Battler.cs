using Game.Core.Attributes;
using Game.Core.Attributes.Modifiers;
using Game.Core.Skills;
using static Game.Core.EAttribute;

namespace Game.Core.Battle
{
    /// <summary>
    /// Encapsulates a combatant for battle simulation.
    /// </summary>
    public class Battler
    {
        private readonly AttributeCollection _attributes;


        /// <summary>
        /// The active timed skill effects, folded to one <see cref="AttributeEffectStack"/> per affected
        /// attribute rather than one record per application. Every application on an attribute shares a single
        /// expiry, so the stack collapses its magnitudes into a single combined modifier per modifier type
        /// (additive amounts summed, multiplicative factors compounded). This keeps each apply and each
        /// per-tick expiry pass O(affected attributes) no matter how deep a buff stacks — a persistently
        /// re-applied buff that never lapses would otherwise add one modifier per fire and make a battle
        /// O(ticks²). Lazily created so a battler never targeted by an effect allocates nothing, and the expiry
        /// pass stays allocation-free on the replay hot path (#286).
        /// </summary>
        private List<AttributeEffectStack>? _attributeStacks;

        /// <summary>
        /// This battler's elapsed simulated time in ms, advanced one tick at a time by
        /// <see cref="AdvanceEffects"/>. Active effects store an absolute expiry against this clock, so expiry
        /// is a comparison rather than a per-tick countdown and <see cref="ActiveEffect"/> stays immutable.
        /// </summary>
        private long _elapsedMs;

        public double CurrentHealth { get; private set; }

        public List<BattleSkill> Skills { get; private set; }

        public int Level { get; private set; }

        public bool IsDead => CurrentHealth <= 0;

        public Battler(AttributeCollection attributes, IEnumerable<Skill> skills, int level)
        {
            _attributes = attributes;
            CurrentHealth = _attributes[MaxHealth];
            Skills = skills.Select(s => new BattleSkill(s)).ToList();
            Level = level;
        }

        public void Update(BattleContext context)
        {
            foreach (var skill in Skills)
            {
                skill.Update(context);
            }
        }

        public double GetCooldownMultiplier()
        {
            // CooldownRecovery is a base-1 multiplier read directly (1.0 = normal charge speed); see
            // StaticAttributeModifiers for the base/derived formula.
            return _attributes[CooldownRecovery];
        }

        public double GetAttributeValue(EAttribute attribute)
        {
            return _attributes[attribute];
        }

        /// <summary>
        /// Amplifies an outgoing <paramref name="rawDamage"/> hit of the given <paramref name="damageType"/> by
        /// this (attacking) battler's amplification: <c>rawDamage × (1 + Σ applies(type).Amplification)</c>, the
        /// additive sum folded in the fixed <see cref="DamageTypes.AmplificationAttributes"/> order so both
        /// simulators agree bit-for-bit. With no amplification authored the sum is <c>0</c>, so the factor is an
        /// exact <c>1.0</c> and the hit is unchanged (the reduce-to-today identity, spike #1320 Area B).
        /// </summary>
        public double AmplifyDamage(double rawDamage, EDamageType damageType)
        {
            var amplification = 0.0;
            var amplificationAttributes = DamageTypes.AmplificationAttributes(damageType);
            for (var i = 0; i < amplificationAttributes.Count; i++)
            {
                amplification += _attributes[amplificationAttributes[i]];
            }

            return rawDamage * (1 + amplification);
        }

        /// <summary>
        /// The net damage an incoming hit of <paramref name="dealt"/> (already amplified and crit-multiplied) of
        /// the given <paramref name="damageType"/> would deal to this (defending) battler, <b>without</b>
        /// mutating health: percentage resistance first (<c>dealt × (1 − Σ applies(type).Resistance)</c>,
        /// <b>unclamped</b> — a negative total amplifies as vulnerability, a total above <c>1</c> drives the
        /// result negative as absorption), then — only while the post-resistance damage is still positive — the
        /// <see cref="EAttribute.Toughness"/> mitigation multiplier <c>(1 − Toughness / (Toughness + K·attackerLevel))</c>.
        /// The toughness curve is a diminishing-returns percentage: effective HP is linear in Toughness while the
        /// reduction asymptotes below <c>100%</c> (no immunity), and <paramref name="attackerLevel"/> scales the
        /// denominator so the band stays stable as content scales (spike #1330). The resistance sum is folded in
        /// the fixed <see cref="DamageTypes.ResistanceAttributes"/> order for parity; with no resistance and no
        /// Toughness the positive branch reduces to <c>dealt</c>. The whole stack is multiplicative — with Block's
        /// flat reduction removed (spike #1330 Area B) there is no flat subtraction left, so the only path to a
        /// negative (absorbing) result is a resistance above <c>1</c>, and no clamp is needed.
        /// </summary>
        public double ComputeNetDamage(double dealt, EDamageType damageType, int attackerLevel)
        {
            var resistance = 0.0;
            var resistanceAttributes = DamageTypes.ResistanceAttributes(damageType);
            for (var i = 0; i < resistanceAttributes.Count; i++)
            {
                resistance += _attributes[resistanceAttributes[i]];
            }

            return NetDamageAfterResistance(dealt, resistance, attackerLevel);
        }

        // The mitigation tail shared by the live hit and the Hex counterfactual: percentage resistance then the
        // Toughness curve (read live off this battler). Factored out (byte-identical to the former inline body)
        // so the Hex tally can measure the same hit against a different resistance — the innate baseline —
        // without duplicating the curve.
        private double NetDamageAfterResistance(double dealt, double resistance, int attackerLevel)
        {
            return NetDamageAfterMitigation(dealt, resistance, _attributes[Toughness], attackerLevel);
        }

        // The full mitigation tail parameterized on both resistance and Toughness, so the Sunder counterfactual
        // can measure the same hit against a different Toughness (the pre-debuff baseline) without duplicating
        // the curve. NetDamageAfterResistance is the live-Toughness instance of this.
        private double NetDamageAfterMitigation(double dealt, double resistance, double toughness, int attackerLevel)
        {
            var mitigated = dealt * (1 - resistance);
            if (mitigated <= 0)
            {
                // Absorption (or a zero hit): the target takes a net heal; the toughness curve does not apply
                // (mitigation can neither heal nor deepen an absorption heal).
                return mitigated;
            }

            return mitigated * (1 - ToughnessReduction(toughness, attackerLevel));
        }

        // Toughness / (Toughness + K·attackerLevel) as a multiplier, so EHP is linear in Toughness and the
        // reduction asymptotes below 100% (a positive hit can never go negative through it). K·attackerLevel
        // keeps the band stable across content scaling. Both simulators must compute this expression identically
        // for battle parity. The Sunder tally's investment measurement (SunderBonusForHit) reads this same helper
        // rather than re-deriving the curve, so the two halves of the tally can't diverge (and a future fix to
        // the curve's unclamped domain below 0, #1461, lands in one place).
        private static double ToughnessReduction(double toughness, int attackerLevel)
        {
            var scaled = GameConstants.ToughnessMitigationConstant * attackerLevel;
            return toughness / (toughness + scaled);
        }

        /// <summary>
        /// The normalized-marginal Hex bonus for a direct hit of <paramref name="dealt"/> (attacker-amplified,
        /// pre-crit) of <paramref name="damageType"/> against this (defending) battler — the extra damage the
        /// <b>opponent-applied</b> vulnerability let through, booked to the attacker's Hex signal (#1427). The
        /// vulnerability <c>v</c> is the opponent's own applied resistance reduction for the type
        /// (<see cref="AppliedVulnerability"/>) — tracked as the modifiers the opponent contributed, so it credits
        /// the <b>work the debuff did</b> regardless of the target's base resistance or its own resistance buffs.
        /// The raw marginal is the live net minus the net the hit <em>would</em> have dealt without that debuff
        /// (live resistance raised back by <c>v</c>), discounted by <c>1/(1 + v)</c> — the same concave saturation
        /// the crit bonus applies to its investment (<c>marginal / (1 + investment)</c>, so a token debuff trains
        /// little and a committed one saturates toward one baseline hit). In the normal region the marginal is
        /// <c>dealt × v × toughnessFactor</c>, flat in the target's resistance — Hex trains the same against a soft
        /// or a resistant enemy (no resist-farming). (That flatness is scoped to the non-absorbing region: if the
        /// without-debuff resistance exceeds <c>1</c>, that baseline hit is a net heal and the marginal folds in
        /// the avoided heal — correctly crediting the debuff for turning a heal into a hit, but no longer flat. No
        /// content authors enemy absorption today.) Measured against the vanilla (pre-crit) hit so it composes with
        /// crit without either overlay inflating the other. Returns <c>0</c> when no vulnerability is applied. A
        /// backend-only side channel — it never mutates health.
        /// </summary>
        public double HexBonusForHit(double dealt, EDamageType damageType, int attackerLevel)
        {
            var vulnerability = AppliedVulnerability(damageType);
            if (vulnerability <= 0)
            {
                return 0;
            }

            var liveResistance = 0.0;
            var resistanceAttributes = DamageTypes.ResistanceAttributes(damageType);
            for (var i = 0; i < resistanceAttributes.Count; i++)
            {
                liveResistance += _attributes[resistanceAttributes[i]];
            }

            // The baseline is the same hit without the opponent's debuff — resistance raised back by v — so the
            // marginal is exactly the damage that debuff enabled (independent of base resistance and enemy buffs).
            var marginal = NetDamageAfterResistance(dealt, liveResistance, attackerLevel)
                - NetDamageAfterResistance(dealt, liveResistance + vulnerability, attackerLevel);
            return marginal > 0 ? marginal / (1 + vulnerability) : 0;
        }

        /// <summary>
        /// The opponent-applied vulnerability on this (defending) battler for <paramref name="damageType"/> — the
        /// total resistance <b>reduction</b> the opponent's timed effects contributed across the type's resistance
        /// attributes, clamped at <c>0</c> (spike #1398 → Hex, #1427). Tracked from the effects the opponent
        /// applied (<see cref="ApplyEffect"/>'s <c>tracksVulnerability</c>) rather than diffed against a baseline,
        /// so it credits the debuff's own work even when the target's base resistance is high or the target buffs
        /// its own resistance (a self-buff never lowers this). Rides the shared per-attribute effect-stack expiry,
        /// so it returns to <c>0</c> for free when the debuff lapses.
        /// </summary>
        public double AppliedVulnerability(EDamageType damageType)
        {
            if (_attributeStacks is null)
            {
                return 0;
            }

            var contribution = 0.0;
            var resistanceAttributes = DamageTypes.ResistanceAttributes(damageType);
            for (var i = 0; i < resistanceAttributes.Count; i++)
            {
                contribution += StackVulnerabilityContribution(resistanceAttributes[i]);
            }

            // Contributions are the signed resistance deltas the opponent applied (negative for a debuff); the
            // vulnerability is their reduction, so negate and clamp — an opponent that only raised resistance is 0.
            return contribution < 0 ? -contribution : 0;
        }

        // The vulnerability-tracked resistance delta the opponent has applied to one attribute, or 0 when no
        // effect stack for it is active. A linear scan over the affected-attribute count, like GetOrCreateStack.
        private double StackVulnerabilityContribution(EAttribute attribute)
        {
            foreach (var stack in _attributeStacks!)
            {
                if (stack.Attribute == attribute)
                {
                    return stack.VulnerabilityContribution;
                }
            }

            return 0;
        }

        /// <summary>
        /// The normalized-marginal Sunder bonus for a direct hit of <paramref name="dealt"/> (attacker-amplified,
        /// pre-crit) of <paramref name="damageType"/> against this (defending) battler — the extra damage the
        /// <b>opponent-applied</b> Toughness debuff let through the mitigation curve, booked to the attacker's
        /// Sunder signal (#1429). The debuff strength <c>s</c> is this battler's opponent-applied Toughness
        /// reduction (<see cref="AppliedSunder"/>). Unlike Hex's flat resistance percentage, Toughness feeds a
        /// diminishing-returns curve, so <c>s</c> raw points are not a meaningful investment unit on their own —
        /// the same point removal is worth more mitigation against a lightly-invested Toughness than a heavily
        /// -invested one. The investment normalized by <c>φ</c> is instead the <b>mitigation-percentage-points
        /// removed</b> — the Toughness curve's own reduction at the baseline (pre-debuff) Toughness minus its
        /// reduction at the live (debuffed) Toughness — which stays bounded in <c>[0, 1)</c> like Hex's resistance
        /// <c>v</c>, so a token Sunder debuff trains little and a committed one saturates the same way. The raw
        /// marginal itself is still the live net minus the net the hit <em>would</em> have dealt at the baseline
        /// Toughness (live resistance unaffected — Sunder only debuffs Toughness), discounted by that same
        /// <c>1/(1 + investment)</c>. Returns <c>0</c> when no Sunder debuff is applied. A backend-only side
        /// channel — it never mutates health. Direct-hit only: DoT bypasses the Toughness curve entirely (a
        /// Toughness debuff cannot affect it), so there is no DoT counterpart to this method.
        /// </summary>
        public double SunderBonusForHit(double dealt, EDamageType damageType, int attackerLevel)
        {
            var sunder = AppliedSunder();
            if (sunder <= 0)
            {
                return 0;
            }

            var liveResistance = 0.0;
            var resistanceAttributes = DamageTypes.ResistanceAttributes(damageType);
            for (var i = 0; i < resistanceAttributes.Count; i++)
            {
                liveResistance += _attributes[resistanceAttributes[i]];
            }

            var liveToughness = _attributes[Toughness];
            var baselineToughness = liveToughness + sunder;
            var marginal = NetDamageAfterMitigation(dealt, liveResistance, liveToughness, attackerLevel)
                - NetDamageAfterMitigation(dealt, liveResistance, baselineToughness, attackerLevel);
            if (marginal <= 0)
            {
                return 0;
            }

            var investment = ToughnessReduction(baselineToughness, attackerLevel) - ToughnessReduction(liveToughness, attackerLevel);
            return investment > 0 ? marginal / (1 + investment) : 0;
        }

        /// <summary>
        /// The opponent-applied Toughness reduction on this (defending) battler — the total <see cref="Toughness"/>
        /// debuff the opponent's timed effects contributed, clamped at <c>0</c> (spike #1398 → Sunder, #1429).
        /// Tracked from the effects the opponent applied (<see cref="ApplyEffect"/>'s <c>tracksSunder</c>) rather
        /// than diffed against a baseline, so it credits the debuff's own work even when the target's base
        /// Toughness is high or the target buffs its own Toughness (a self-buff never lowers this). Toughness is
        /// untyped (unlike resistance), so this takes no damage-type parameter. Rides the shared per-attribute
        /// effect-stack expiry, so it returns to <c>0</c> for free when the debuff lapses.
        /// </summary>
        public double AppliedSunder()
        {
            var contribution = StackSunderContribution(Toughness);
            return contribution < 0 ? -contribution : 0;
        }

        // The Sunder-tracked Toughness delta the opponent has applied, or 0 when no effect stack for Toughness is
        // active. Null-safe (rather than relying on a caller guard) since AppliedSunder is the only caller.
        private double StackSunderContribution(EAttribute attribute)
        {
            if (_attributeStacks is null)
            {
                return 0;
            }

            foreach (var stack in _attributeStacks)
            {
                if (stack.Attribute == attribute)
                {
                    return stack.SunderContribution;
                }
            }

            return 0;
        }

        /// <summary>
        /// This (attacking) battler's own applied ramp on <paramref name="damageType"/> — the total amplification
        /// its <b>own</b> timed self-buffs contributed across the type's amplification attributes (spike #1398 →
        /// Momentum, #1428). Tracked from the effects this battler applied to itself
        /// (<see cref="ApplyEffect"/>'s <c>tracksMomentum</c>), so it isolates the ramp's own contribution from
        /// any static (item/base) amplification the battler already carries. Rides the shared per-attribute
        /// effect-stack expiry, so it returns to <c>0</c> for free when the ramp lapses.
        /// </summary>
        public double AppliedMomentum(EDamageType damageType)
        {
            if (_attributeStacks is null)
            {
                return 0;
            }

            var contribution = 0.0;
            var amplificationAttributes = DamageTypes.AmplificationAttributes(damageType);
            for (var i = 0; i < amplificationAttributes.Count; i++)
            {
                contribution += StackMomentumContribution(amplificationAttributes[i]);
            }

            return contribution > 0 ? contribution : 0;
        }

        // The ramp-tracked amplification this battler has applied to one of its own attributes, or 0 when no
        // effect stack for it is active. A linear scan over the affected-attribute count, like GetOrCreateStack.
        private double StackMomentumContribution(EAttribute attribute)
        {
            foreach (var stack in _attributeStacks!)
            {
                if (stack.Attribute == attribute)
                {
                    return stack.MomentumContribution;
                }
            }

            return 0;
        }

        /// <summary>
        /// Applies an incoming hit of <paramref name="dealt"/> (already amplified and crit-multiplied) of the
        /// given <paramref name="damageType"/> via <see cref="ComputeNetDamage"/> — percentage resistance then
        /// the <see cref="EAttribute.Toughness"/> mitigation curve (scaled by the <paramref name="attackerLevel"/>).
        /// Returns the net damage dealt; a negative result (absorption) heals this battler, <b>capped at
        /// <see cref="MaxHealth"/></b> — the game has no overheal/shield concept, so this matches
        /// <see cref="ApplyHealOverTime"/> rather than letting the reactive absorption channel bank health above
        /// the cap.
        /// </summary>
        public double TakeDamage(double dealt, EDamageType damageType, int attackerLevel)
        {
            var net = ComputeNetDamage(dealt, damageType, attackerLevel);
            if (net < 0)
            {
                // Absorption: cap the heal at the remaining room to MaxHealth (consistent with ApplyHealOverTime),
                // and report the actual healed amount so the per-skill / global stats stay reconciled.
                var room = _attributes[MaxHealth] - CurrentHealth;
                var heal = -net < room ? -net : room;
                heal = heal > 0 ? heal : 0;
                CurrentHealth += heal;
                return -heal;
            }

            CurrentHealth -= net;
            return net;
        }

        /// <summary>
        /// Subtracts <paramref name="amount"/> of reflected damage directly from this (attacking) battler's
        /// health, <b>bypassing all of its own mitigation</b> (resistance and the Toughness curve) — the
        /// deterministic damage-reflection channel (spike #1330). The caller resolves the amount
        /// (defender net × the defender's <see cref="EAttribute.DamageReflection"/>) and reflects only a
        /// positive hit, so this is a raw health subtraction with no floor or cap.
        /// </summary>
        public void TakeReflectedDamage(double amount)
        {
            CurrentHealth -= amount;
        }

        /// <summary>
        /// Applies one tick of typed damage-over-time (spike #1320, Area C). Loops the DoT types in the fixed
        /// <see cref="DamageTypes.DotAccumulators"/> order, scaling each type's per-second accumulator to
        /// <paramref name="ms"/> and applying this (defending) battler's resistance for that type <b>sampled
        /// live</b> — <c>perSec × ms/1000 × (1 − Σ applies(type).Resistance)</c> — so a vulnerability debuff
        /// makes existing DoTs hurt immediately. The caster's amplification was already frozen into the
        /// accumulator at apply time (<see cref="BattleContext.ApplySkillEffect"/>). Unlike
        /// <see cref="TakeDamage"/> it <b>bypasses the Toughness curve</b> — resistance is its only mitigation —
        /// and is never reflected (reflection is scoped to direct hits, spike #1330); it returns the total damage
        /// dealt so the caller can attribute it to the battle statistics. With no DoT authored every accumulator
        /// is <c>0</c>, so the loop adds nothing and the return is an exact <c>0</c>.
        /// </summary>
        /// <remarks>
        /// Intentionally <b>not</b> floored at zero. DoT bypasses mitigation entirely, so a tick goes negative
        /// only through a deliberately authored negative accumulator or a resistance above <c>1</c> (absorption)
        /// — and a floor wouldn't prevent that, it would just silently rewrite the value. Authored healing
        /// belongs in the capped <see cref="ApplyHealOverTime"/> channel instead. The resistance
        /// sum is folded in the fixed <see cref="DamageTypes.ResistanceAttributes"/> order, and each type's
        /// contribution is summed in <see cref="DamageTypes.DotAccumulators"/> order, so both simulators agree
        /// bit-for-bit. A single typed DoT with no resistance reduces to <c>perSec × ms/1000</c> — byte-identical
        /// to the former single-accumulator outcome (the reduce-to-today identity).
        /// </remarks>
        /// <param name="ms">The elapsed simulated time this tick.</param>
        /// <param name="recordExposure">
        /// Optional per-type <b>pre-mitigation</b> recorder for the proficiency incoming book (spike #1337) —
        /// invoked with each DoT type and its tick damage <em>before</em> this battler's resistance. Supplied
        /// only when this battler's exposure is tracked (the player); <c>null</c> leaves the loop unchanged. It
        /// is a backend-only side channel that never touches the health math, so it adds no parity surface.
        /// </param>
        /// <param name="recordDamageDealt">
        /// Optional per-type <b>post-mitigation</b> recorder for the proficiency offense book (spike #1338) —
        /// invoked with each DoT type and the tick damage <em>after</em> this battler's resistance, the same
        /// value the tick subtracts from health. Supplied when this battler is the victim of the player's DoT
        /// (the enemy), so the player's DoT damage dealt is typed for the offense binding consistently with a
        /// direct hit's post-mitigation actual damage; <c>null</c> leaves the loop unchanged. Like
        /// <paramref name="recordExposure"/> it is a backend-only side channel that adds no parity surface.
        /// </param>
        /// <param name="recordHexBonus">
        /// Optional recorder for the attacker's Hex signal (#1427) — invoked per DoT type with the
        /// normalized-marginal damage the opponent-applied vulnerability enabled this tick (type-neutral, so it
        /// takes just the amount). Supplied only when this battler is the victim of the player's DoT (the enemy);
        /// <c>null</c> skips the vulnerability lookup entirely. A backend-only side channel like the others.
        /// </param>
        public double ApplyDamageOverTime(
            int ms,
            Action<EDamageType, double>? recordExposure = null,
            Action<EDamageType, double>? recordDamageDealt = null,
            Action<double>? recordHexBonus = null)
        {
            var dot = 0.0;
            var accumulators = DamageTypes.DotAccumulators;
            for (var i = 0; i < accumulators.Count; i++)
            {
                var perSecond = _attributes[accumulators[i].Accumulator];
                if (perSecond == 0)
                {
                    continue;
                }

                // The pre-mitigation tick (before this battler's resistance) is its exposure to this DoT type;
                // record it for the incoming book when a recorder is supplied. Folded out of the dot sum below
                // so the recorded value and the mitigated value share one multiplication (no parity drift).
                var preMitigation = perSecond * ms / 1000.0;
                recordExposure?.Invoke(accumulators[i].Type, preMitigation);

                var resistance = 0.0;
                var resistanceAttributes = DamageTypes.ResistanceAttributes(accumulators[i].Type);
                for (var j = 0; j < resistanceAttributes.Count; j++)
                {
                    resistance += _attributes[resistanceAttributes[j]];
                }

                // The post-resistance tick is both what the health loses and what the attacker dealt of this
                // type; compute it once so the recorded damage-dealt and the health math cannot drift.
                var tickDamage = preMitigation * (1 - resistance);
                recordDamageDealt?.Invoke(accumulators[i].Type, tickDamage);

                // The attacker's Hex bonus for this tick: the opponent-applied vulnerability v (the resistance the
                // attacker's own debuff removed) let through preMitigation × v, discounted by the same 1/(1 + v)
                // saturation the direct-hit and crit bonuses use. DoT bypasses the Toughness curve, so the enabled
                // damage is just preMitigation × v — flat in this battler's resistance.
                if (recordHexBonus is not null)
                {
                    var vulnerability = AppliedVulnerability(accumulators[i].Type);
                    if (vulnerability > 0)
                    {
                        recordHexBonus(preMitigation * vulnerability / (1 + vulnerability));
                    }
                }

                dot += tickDamage;
            }

            CurrentHealth -= dot;
            return dot;
        }

        /// <summary>
        /// Applies one tick of heal-over-time from <see cref="HealthRegenPerSecond"/> (authored per second,
        /// scaled to <paramref name="ms"/>), capped at <see cref="MaxHealth"/>. Returns the actual (post-cap)
        /// health restored so the caller can attribute it to the battle statistics.
        /// </summary>
        public double ApplyHealOverTime(int ms)
        {
            var heal = _attributes[HealthRegenPerSecond] * ms / 1000.0;
            var healed = Math.Min(heal, _attributes[MaxHealth] - CurrentHealth);
            if (healed > 0)
            {
                CurrentHealth += healed;
                return healed;
            }

            return 0;
        }

        /// <summary>
        /// Applies <paramref name="effect"/> as a timed attribute modifier on this battler, using the
        /// already-resolved <paramref name="amount"/> as its magnitude (the caster's attribute scaling is
        /// applied by <see cref="BattleContext.ApplySkillEffect"/> before this is reached). Each application
        /// <b>stacks</b>: its magnitude folds into the attribute's single combined modifier for the effect's
        /// type (additive amounts add, multiplicative factors compound). All active applications targeting the
        /// <b>same attribute</b> share a single expiry: applying any effect on that attribute resets the whole
        /// stack to this application's duration, so it expires together with no independent per-portion
        /// expirations (#992 / #740). A new modifier may shift <see cref="MaxHealth"/>, so the health is
        /// re-clamped.
        /// <para>
        /// When <paramref name="tracksVulnerability"/> is set (an opponent-applied resistance debuff — the Hex
        /// enabler, #1427), the resolved additive <paramref name="amount"/> is also accumulated onto the stack's
        /// <see cref="AttributeEffectStack.VulnerabilityContribution"/>, so <see cref="AppliedVulnerability"/> can
        /// credit the debuff's own work independently of the target's base resistance or its own buffs. It rides
        /// the shared stack expiry — cleared for free when the stack lapses — and is a backend-only side channel
        /// that never touches the combined modifier or the health math, so it adds no parity surface.
        /// </para>
        /// <para>
        /// When <paramref name="tracksMomentum"/> is set (a self-applied amplification ramp — the Momentum
        /// enabler, #1428), <paramref name="amount"/> is likewise accumulated onto the stack's
        /// <see cref="AttributeEffectStack.MomentumContribution"/>, so <see cref="AppliedMomentum"/> can isolate
        /// the ramp's own contribution from any static amplification the battler already carries. Same shared
        /// expiry, same no-parity-surface side channel as the vulnerability tracker above.
        /// </para>
        /// <para>
        /// When <paramref name="tracksSunder"/> is set (an opponent-applied Toughness debuff — the Sunder
        /// enabler, #1429), <paramref name="amount"/> is likewise accumulated onto the stack's
        /// <see cref="AttributeEffectStack.SunderContribution"/>, so <see cref="AppliedSunder"/> can credit the
        /// debuff's own work independently of the target's base Toughness or its own buffs. Same shared expiry,
        /// same no-parity-surface side channel as the trackers above.
        /// </para>
        /// </summary>
        public void ApplyEffect(
            SkillEffect effect, double amount,
            bool tracksVulnerability = false, bool tracksMomentum = false, bool tracksSunder = false)
        {
            _attributeStacks ??= [];
            var stack = GetOrCreateStack(effect.AttributeId);

            // Re-applying any effect on this attribute resets the whole stack's shared expiry to the new
            // application's duration (it may extend a longer-lived application or cut a shorter one short).
            stack.ExpiresAtMs = _elapsedMs + effect.DurationMs;

            // An opponent-applied resistance debuff also accrues its signed delta to the vulnerability tally the
            // Hex signal reads — separate from the combined modifier below, so the health math is untouched.
            if (tracksVulnerability)
            {
                stack.VulnerabilityContribution += amount;
            }

            // A self-applied amplification ramp likewise accrues its delta to the Momentum tally's contribution
            // tracker — separate from the combined modifier below, so the health math is untouched.
            if (tracksMomentum)
            {
                stack.MomentumContribution += amount;
            }

            // An opponent-applied Toughness debuff likewise accrues its signed delta to the Sunder tally's
            // contribution tracker — separate from the combined modifier below, so the health math is untouched.
            if (tracksSunder)
            {
                stack.SunderContribution += amount;
            }

            // Fold the application into the attribute's single combined modifier for its type, swapping the old
            // combined instance for the new one. The collection therefore holds at most one effect modifier per
            // (attribute, type) regardless of how deep the stack runs.
            if (effect.ModifierType is EModifierType.Multiplicative)
            {
                var combined = (stack.Multiplicative?.Amount ?? 1.0) * amount;
                stack.Multiplicative = SwapCombinedModifier(
                    stack.Multiplicative, effect.AttributeId, EModifierType.Multiplicative, combined);
            }
            else
            {
                var combined = (stack.Additive?.Amount ?? 0.0) + amount;
                stack.Additive = SwapCombinedModifier(
                    stack.Additive, effect.AttributeId, EModifierType.Additive, combined);
            }

            ClampHealthToMaxHealth();
        }

        /// <summary>
        /// Advances this battler's simulated-time clock by <paramref name="ms"/> and removes any attribute
        /// stack whose shared expiry has been reached (its combined modifiers removed and health re-clamped).
        /// Called at the start of each tick before any skill fires, so an effect influences exactly
        /// <c>DurationMs / tickSize</c> ticks counting the one it was applied on.
        /// </summary>
        public void AdvanceEffects(int ms)
        {
            // Advance the clock every tick, even with no active effects, so an effect applied on a later tick
            // still computes its absolute expiry from the correct elapsed time.
            _elapsedMs += ms;

            if (_attributeStacks is null || _attributeStacks.Count == 0)
            {
                return;
            }

            var removedAny = false;
            for (var i = _attributeStacks.Count - 1; i >= 0; i--)
            {
                var stack = _attributeStacks[i];
                if (stack.ExpiresAtMs <= _elapsedMs)
                {
                    if (stack.Additive is not null)
                    {
                        _attributes.RemoveModifier(stack.Additive);
                    }

                    if (stack.Multiplicative is not null)
                    {
                        _attributes.RemoveModifier(stack.Multiplicative);
                    }

                    _attributeStacks.RemoveAt(i);
                    removedAny = true;
                }
            }

            if (removedAny)
            {
                ClampHealthToMaxHealth();
            }
        }

        /// <summary>
        /// Clamps <see cref="CurrentHealth"/> down to <see cref="MaxHealth"/> when an effect change has dropped
        /// the maximum below it; a rise in MaxHealth leaves CurrentHealth untouched (no free healing).
        /// </summary>
        private void ClampHealthToMaxHealth()
        {
            var maxHealth = _attributes[MaxHealth];
            if (CurrentHealth > maxHealth)
            {
                CurrentHealth = maxHealth;
            }
        }

        // Returns the stack for the given attribute, creating it on first use. The scan is over the
        // affected-attribute count (≤ the attribute count), never the application count, so it stays cheap.
        private AttributeEffectStack GetOrCreateStack(EAttribute attribute)
        {
            foreach (var stack in _attributeStacks!)
            {
                if (stack.Attribute == attribute)
                {
                    return stack;
                }
            }

            var created = new AttributeEffectStack(attribute);
            _attributeStacks.Add(created);
            return created;
        }

        // Removes the previous combined modifier (if any) and adds the new one, returning it to be stored back
        // on the stack. AttributeModifier is immutable, so the running magnitude is carried by swapping whole
        // instances — keeping a single effect modifier per (attribute, type) in the collection.
        private AttributeModifier SwapCombinedModifier(
            AttributeModifier? existing, EAttribute attribute, EModifierType type, double amount)
        {
            if (existing is not null)
            {
                _attributes.RemoveModifier(existing);
            }

            var modifier = new AttributeModifier
            {
                Attribute = attribute,
                Amount = amount,
                Type = type,
                Source = EAttributeModifierSource.SkillEffect,
            };
            _attributes.AddModifier(modifier);
            return modifier;
        }

        /// <summary>
        /// The folded active-effect state for one attribute: the absolute simulated time (in
        /// <see cref="_elapsedMs"/> ms) at which the whole stack expires — shared by every application on the
        /// attribute and reset to the newest application's duration (see <see cref="ApplyEffect"/>) — plus the
        /// single combined modifier currently in the collection for each modifier type (null when no
        /// application of that type is active). Folding the applications keeps the per-tick expiry pass and
        /// per-fire application bounded by the affected-attribute count rather than the unbounded stack depth.
        /// </summary>
        private sealed class AttributeEffectStack(EAttribute attribute)
        {
            public EAttribute Attribute { get; } = attribute;
            public long ExpiresAtMs { get; set; }
            public AttributeModifier? Additive { get; set; }
            public AttributeModifier? Multiplicative { get; set; }

            /// <summary>
            /// The signed resistance delta an opponent's tracked debuffs contributed to this attribute (negative
            /// for a vulnerability), summed across applications and read by <see cref="AppliedVulnerability"/> for
            /// the Hex tally (#1427). Separate from the combined modifiers above so it never touches the health
            /// math, and cleared with the stack on the shared expiry. <c>0</c> for any non-debuffed attribute.
            /// </summary>
            public double VulnerabilityContribution { get; set; }

            /// <summary>
            /// The amplification this battler's own tracked ramp applications contributed to this attribute,
            /// summed across applications and read by <see cref="AppliedMomentum"/> for the Momentum tally
            /// (#1428). Separate from the combined modifiers above so it never touches the health math, and
            /// cleared with the stack on the shared expiry. <c>0</c> for any non-ramped attribute.
            /// </summary>
            public double MomentumContribution { get; set; }

            /// <summary>
            /// The signed Toughness delta an opponent's tracked debuffs contributed to this attribute (negative
            /// for a Sunder debuff), summed across applications and read by <see cref="AppliedSunder"/> for the
            /// Sunder tally (#1429). Separate from the combined modifiers above so it never touches the health
            /// math, and cleared with the stack on the shared expiry. Only ever populated on the Toughness stack.
            /// </summary>
            public double SunderContribution { get; set; }
        }
    }
}
