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
        /// Applies <paramref name="rawDamage"/> after subtracting flat <see cref="Defense"/> and the optional
        /// <paramref name="blockReduction"/> (a second flat reduction in the same clamp, supplied by the
        /// caller only when an incoming hit is blocked), never below zero. Returns the damage actually dealt.
        /// </summary>
        public double TakeDamage(double rawDamage, double blockReduction = 0)
        {
            var damage = rawDamage - _attributes[Defense] - blockReduction;
            damage = damage > 0 ? damage : 0;
            CurrentHealth -= damage;
            return damage;
        }

        /// <summary>
        /// Applies one tick of damage-over-time from <see cref="DamageTakenPerSecond"/> (authored per second,
        /// scaled to <paramref name="ms"/>). Unlike <see cref="TakeDamage"/> it <b>bypasses Defense</b>, and
        /// returns the damage dealt so the caller can attribute it to the battle statistics.
        /// </summary>
        /// <remarks>
        /// Intentionally <b>not</b> floored at zero, unlike <see cref="TakeDamage"/>. That floor exists only so
        /// Defense mitigation can't drive net damage below zero and turn a hit into a heal; DoT has no mitigation
        /// step, so a tick is negative only if a negative <see cref="DamageTakenPerSecond"/> is deliberately
        /// authored — and a floor wouldn't prevent that, it would just silently rewrite the authored value to
        /// zero. Authored healing belongs in the capped <see cref="ApplyHealOverTime"/> channel instead.
        /// </remarks>
        public double ApplyDamageOverTime(int ms)
        {
            var damage = _attributes[DamageTakenPerSecond] * ms / 1000.0;
            CurrentHealth -= damage;
            return damage;
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
