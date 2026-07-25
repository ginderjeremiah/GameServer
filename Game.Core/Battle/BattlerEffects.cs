using Game.Core.Attributes;
using Game.Core.Attributes.Modifiers;
using Game.Core.Skills;
using static Game.Core.EAttribute;

namespace Game.Core.Battle
{
    /// <summary>
    /// One <see cref="Battler"/>'s timed skill-effect bookkeeping: the per-attribute effect stacks, their
    /// shared simulated-time clock, and the backend-only overlay tallies that read the contributions those
    /// stacks accrue. Split out of <see cref="Battler"/> (#2380) so the battler keeps only the
    /// parity-critical health/mitigation math — this machinery is bounded-cost bookkeeping around it, and the
    /// overlay readers are a backend-only side channel with no frontend mirror at all.
    /// <para>
    /// Only <see cref="Apply"/> and <see cref="Advance"/> are parity-relevant (they own the stacking, shared
    /// expiry and combined-modifier folding the frontend mirrors in <c>battler.ts</c>); both report back to the
    /// battler rather than touching health, since the MaxHealth re-clamp is the battler's own concern — and both
    /// are <c>internal</c> so the only way in from outside the domain is <see cref="Battler.ApplyEffect"/> /
    /// <see cref="Battler.AdvanceEffects"/>, which cannot skip that re-clamp.
    /// </para>
    /// </summary>
    public sealed class BattlerEffects
    {
        private readonly AttributeCollection _attributes;

        internal BattlerEffects(AttributeCollection attributes)
        {
            _attributes = attributes;
        }

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
        /// The owning battler's elapsed simulated time in ms, advanced one tick at a time by
        /// <see cref="Advance"/>. Active effects store an absolute expiry against this clock, so expiry is a
        /// comparison rather than a per-tick countdown.
        /// </summary>
        private long _elapsedMs;

        /// <summary>
        /// The shared overlay-tally saturation <c>φ(a) = a / (1 + a)</c>, applied to an overlay's own investment
        /// magnitude when booking its share claim (#1481): ~linear at the low end so a token investment trains
        /// proportionally little, asymptoting to <c>1</c> so even a huge investment claims at most the full booked
        /// hit. Every overlay signal (crit, Hex, Momentum, Sunder, Cull, Cadence) uses it on its own investment.
        /// </summary>
        public static double NormalizeInvestment(double investment)
        {
            return investment / (1.0 + investment);
        }

        /// <summary>
        /// Applies <paramref name="effect"/> as a timed attribute modifier, using the already-resolved
        /// <paramref name="amount"/> as its magnitude (the caster's attribute scaling is applied by
        /// <see cref="BattleContext.ApplySkillEffect"/> before this is reached). Each application <b>stacks</b>:
        /// its magnitude folds into the attribute's single combined modifier for the effect's type (additive
        /// amounts add, multiplicative factors compound). All active applications targeting the <b>same
        /// attribute</b> share a single expiry: applying any effect on that attribute resets the whole stack to
        /// this application's duration, so it expires together with no independent per-portion expirations
        /// (#992 / #740). A new modifier may shift <see cref="MaxHealth"/>, so the caller
        /// (<see cref="Battler.ApplyEffect"/>) re-clamps the health afterwards.
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
        internal void Apply(
            SkillEffect effect, double amount,
            bool tracksVulnerability = false, bool tracksMomentum = false, bool tracksSunder = false)
        {
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
        }

        /// <summary>
        /// Advances the simulated-time clock by <paramref name="ms"/> and removes any attribute stack whose
        /// shared expiry has been reached, taking its combined modifiers out of the attribute collection.
        /// Returns whether anything expired, so the caller (<see cref="Battler.AdvanceEffects"/>) re-clamps the
        /// health only when a removal could have dropped MaxHealth. Called at the start of each tick before any
        /// skill fires, so an effect influences exactly <c>DurationMs / tickSize</c> ticks counting the one it
        /// was applied on.
        /// </summary>
        internal bool Advance(int ms)
        {
            // Advance the clock every tick, even with no active effects, so an effect applied on a later tick
            // still computes its absolute expiry from the correct elapsed time.
            _elapsedMs += ms;

            if (_attributeStacks is null || _attributeStacks.Count == 0)
            {
                return false;
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

            return removedAny;
        }

        /// <summary>
        /// The Hex bonus for a hit that booked <paramref name="bookedNet"/> (the post-mitigation damage capped at
        /// the health it actually removed, #1482) of <paramref name="damageType"/> against the battler these
        /// effects belong to — the attacker's Hex signal (#1427), booked as <c>bookedNet × φ(v)</c>
        /// (<see cref="NormalizeInvestment"/>). The vulnerability <c>v</c> is the opponent's own applied resistance
        /// reduction for the type (<see cref="AppliedVulnerability"/>) — tracked as the modifiers the opponent
        /// contributed, so it credits the <b>work the debuff did</b> regardless of the target's base resistance or
        /// its own resistance buffs. A <b>share claim on the damage that actually landed</b>, not a counterfactual
        /// marginal (#1481): the booked nets over a won battle are bounded by the defender's health pool, so the
        /// per-battle claim is ≈ the debuff's coverage share of that pool × <c>φ(v)</c> — enemy-independent at the
        /// accrual level, proportional to the investment through <c>φ</c>, and computed with no counterfactual
        /// curve evaluation. Returns <c>0</c> when no vulnerability is applied. A backend-only side channel — it
        /// never mutates health.
        /// </summary>
        public double HexBonusForHit(double bookedNet, EDamageType damageType)
        {
            var vulnerability = AppliedVulnerability(damageType);
            if (vulnerability <= 0)
            {
                return 0;
            }

            return bookedNet * NormalizeInvestment(vulnerability);
        }

        /// <summary>
        /// The opponent-applied vulnerability on the battler these effects belong to for
        /// <paramref name="damageType"/> — the total resistance <b>reduction</b> the opponent's timed effects
        /// contributed across the type's resistance attributes, clamped at <c>0</c> (spike #1398 → Hex, #1427).
        /// Tracked from the effects the opponent applied (<see cref="Apply"/>'s <c>tracksVulnerability</c>) rather
        /// than diffed against a baseline, so it credits the debuff's own work even when the target's base
        /// resistance is high or the target buffs its own resistance (a self-buff never lowers this). Rides the
        /// shared per-attribute stack expiry, so it returns to <c>0</c> for free when the debuff lapses.
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
                contribution += StackContribution(resistanceAttributes[i], stack => stack.VulnerabilityContribution);
            }

            // Contributions are the signed resistance deltas the opponent applied (negative for a debuff); the
            // vulnerability is their reduction, so negate and clamp — an opponent that only raised resistance is 0.
            return contribution < 0 ? -contribution : 0;
        }

        /// <summary>
        /// The Sunder bonus for a hit that booked <paramref name="bookedNet"/> (the post-mitigation damage capped
        /// at the health it actually removed, #1482) against the battler these effects belong to — the attacker's
        /// Sunder signal (#1429), booked as <c>bookedNet × φ(investment)</c> (<see cref="NormalizeInvestment"/>),
        /// where the investment is the opponent-applied Toughness reduction (<see cref="AppliedSunder"/>) made
        /// dimensionless by the curve's own characteristic magnitude
        /// (<see cref="GameConstants.ToughnessMitigationConstant"/>) — the same constant the live mitigation curve
        /// divides by. The same share-claim shape as every overlay tally (#1481; Sunder pioneered the
        /// no-counterfactual proxy because the nonlinear Toughness curve has no target-flat marginal). Returns
        /// <c>0</c> when no Sunder debuff is applied. A backend-only side channel — it never mutates health.
        /// Direct-hit only: DoT bypasses the Toughness curve entirely (a Toughness debuff cannot affect it), so
        /// there is no DoT counterpart to this method.
        /// </summary>
        public double SunderBonusForHit(double bookedNet)
        {
            var sunder = AppliedSunder();
            if (sunder <= 0)
            {
                return 0;
            }

            return bookedNet * NormalizeInvestment(sunder / GameConstants.ToughnessMitigationConstant);
        }

        /// <summary>
        /// The opponent-applied Toughness reduction on the battler these effects belong to — the total
        /// <see cref="Toughness"/> debuff the opponent's timed effects contributed, clamped at <c>0</c> (spike
        /// #1398 → Sunder, #1429). Tracked from the effects the opponent applied (<see cref="Apply"/>'s
        /// <c>tracksSunder</c>) rather than diffed against a baseline, so it credits the debuff's own work even
        /// when the target's base Toughness is high or the target buffs its own Toughness (a self-buff never
        /// lowers this). Toughness is untyped and a single attribute (unlike resistance's per-type keys), so this
        /// reads one stack directly rather than folding over a type's resistance attributes. Rides the shared
        /// per-attribute stack expiry, so it returns to <c>0</c> for free when the debuff lapses.
        /// </summary>
        public double AppliedSunder()
        {
            var contribution = StackContribution(Toughness, stack => stack.SunderContribution);
            return contribution < 0 ? -contribution : 0;
        }

        /// <summary>
        /// The battler's own applied ramp on <paramref name="damageType"/> — the total amplification its
        /// <b>own</b> timed self-buffs contributed across the type's amplification attributes (spike #1398 →
        /// Momentum, #1428). Tracked from the effects the battler applied to itself (<see cref="Apply"/>'s
        /// <c>tracksMomentum</c>), so it isolates the ramp's own contribution from any static (item/base)
        /// amplification the battler already carries. Rides the shared per-attribute stack expiry, so it returns
        /// to <c>0</c> for free when the ramp lapses.
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
                contribution += StackContribution(amplificationAttributes[i], stack => stack.MomentumContribution);
            }

            return contribution > 0 ? contribution : 0;
        }

        // The tracked contribution (selected via <paramref name="selector"/>) an active effect stack has
        // accrued for one attribute, or 0 when no stack for it exists yet. A linear scan over the
        // affected-attribute count, like GetOrCreateStack. Shared by the per-overlay applied-* readers
        // (AppliedVulnerability, AppliedMomentum, ...) so each just supplies its own contribution field.
        private double StackContribution(EAttribute attribute, Func<AttributeEffectStack, double> selector)
        {
            if (_attributeStacks is null)
            {
                return 0;
            }

            foreach (var stack in _attributeStacks)
            {
                if (stack.Attribute == attribute)
                {
                    return selector(stack);
                }
            }

            return 0;
        }

        // Returns the stack for the given attribute, creating it (and the backing list) on first use. The scan
        // is over the affected-attribute count (≤ the attribute count), never the application count, so it
        // stays cheap.
        private AttributeEffectStack GetOrCreateStack(EAttribute attribute)
        {
            _attributeStacks ??= [];
            foreach (var stack in _attributeStacks)
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
        /// attribute and reset to the newest application's duration (see <see cref="Apply"/>) — plus the
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
