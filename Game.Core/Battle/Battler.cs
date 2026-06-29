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
        /// <see cref="EAttribute.Toughness"/> mitigation multiplier <c>(1 − Toughness / (Toughness + K·attackerLevel))</c>
        /// and finally the optional flat <paramref name="blockReduction"/> (floored at <c>0</c>). The toughness
        /// curve is a diminishing-returns percentage: effective HP is linear in Toughness while the reduction
        /// asymptotes below <c>100%</c> (no immunity), and <paramref name="attackerLevel"/> scales the denominator
        /// so the band stays stable as content scales (spike #1330). The resistance sum is folded in the fixed
        /// <see cref="DamageTypes.ResistanceAttributes"/> order for parity; with no resistance and no Toughness the
        /// positive branch reduces to <c>dealt − blockReduction</c>.
        /// </summary>
        public double ComputeNetDamage(double dealt, EDamageType damageType, int attackerLevel, double blockReduction = 0)
        {
            var resistance = 0.0;
            var resistanceAttributes = DamageTypes.ResistanceAttributes(damageType);
            for (var i = 0; i < resistanceAttributes.Count; i++)
            {
                resistance += _attributes[resistanceAttributes[i]];
            }

            var mitigated = dealt * (1 - resistance);
            if (mitigated <= 0)
            {
                // Absorption (or a zero hit): the target takes a net heal; neither the toughness curve nor flat
                // reduction applies (mitigation can neither heal nor deepen an absorption heal).
                return mitigated;
            }

            // Toughness mitigation: Toughness / (Toughness + K·attackerLevel) as a multiplier, so EHP is linear in
            // Toughness and the reduction asymptotes below 100%. K·attackerLevel keeps the band stable across
            // content scaling. Both simulators must compute this expression identically for battle parity.
            var toughness = _attributes[Toughness];
            var scaled = GameConstants.ToughnessMitigationConstant * attackerLevel;
            var toughnessReduction = toughness / (toughness + scaled);

            var net = mitigated * (1 - toughnessReduction) - blockReduction;
            return net > 0 ? net : 0;
        }

        /// <summary>
        /// Applies an incoming hit of <paramref name="dealt"/> (already amplified and crit-multiplied) of the
        /// given <paramref name="damageType"/> via <see cref="ComputeNetDamage"/> — percentage resistance, the
        /// <see cref="EAttribute.Toughness"/> mitigation curve (scaled by the <paramref name="attackerLevel"/>),
        /// then the optional flat <paramref name="blockReduction"/> (supplied only when the hit is blocked).
        /// Returns the net damage dealt; a negative result (absorption) heals this battler, <b>capped at
        /// <see cref="MaxHealth"/></b> — the game has no overheal/shield concept, so this matches
        /// <see cref="ApplyHealOverTime"/> rather than letting the reactive absorption channel bank health above
        /// the cap.
        /// </summary>
        public double TakeDamage(double dealt, EDamageType damageType, int attackerLevel, double blockReduction = 0)
        {
            var net = ComputeNetDamage(dealt, damageType, attackerLevel, blockReduction);
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
        /// Applies one tick of typed damage-over-time (spike #1320, Area C). Loops the DoT types in the fixed
        /// <see cref="DamageTypes.DotAccumulators"/> order, scaling each type's per-second accumulator to
        /// <paramref name="ms"/> and applying this (defending) battler's resistance for that type <b>sampled
        /// live</b> — <c>perSec × ms/1000 × (1 − Σ applies(type).Resistance)</c> — so a vulnerability debuff
        /// makes existing DoTs hurt immediately. The caster's amplification was already frozen into the
        /// accumulator at apply time (<see cref="BattleContext.ApplySkillEffect"/>). Unlike
        /// <see cref="TakeDamage"/> it <b>bypasses the Toughness curve and flat block</b> — resistance is its only
        /// mitigation — and returns the total damage dealt so the caller can attribute it to the battle statistics. With no DoT authored
        /// every accumulator is <c>0</c>, so the loop adds nothing and the return is an exact <c>0</c>.
        /// </summary>
        /// <remarks>
        /// Intentionally <b>not</b> floored at zero, unlike <see cref="TakeDamage"/>. That floor exists only so the
        /// flat block reduction can't drive net damage below zero and turn a hit into a heal; DoT bypasses
        /// mitigation entirely, so a tick goes negative only through a deliberately authored negative accumulator or a resistance above
        /// <c>1</c> (absorption) — and a floor wouldn't prevent that, it would just silently rewrite the value.
        /// Authored healing belongs in the capped <see cref="ApplyHealOverTime"/> channel instead. The resistance
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
        public double ApplyDamageOverTime(int ms, Action<EDamageType, double>? recordExposure = null)
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

                dot += preMitigation * (1 - resistance);
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
        /// </summary>
        public void ApplyEffect(SkillEffect effect, double amount)
        {
            _attributeStacks ??= [];
            var stack = GetOrCreateStack(effect.AttributeId);

            // Re-applying any effect on this attribute resets the whole stack's shared expiry to the new
            // application's duration (it may extend a longer-lived application or cut a shorter one short).
            stack.ExpiresAtMs = _elapsedMs + effect.DurationMs;

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
        }
    }
}
