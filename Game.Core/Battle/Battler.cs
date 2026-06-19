using Game.Core.Attributes;
using Game.Core.Attributes.Modifiers;
using Game.Core.Players;
using Game.Core.Players.Inventories;
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
        /// The timed skill effects currently modifying this battler's attributes. Lazily created so a
        /// battler that is never targeted by an effect allocates nothing (per-tick bookkeeping in
        /// <see cref="AdvanceEffects"/> stays allocation-free on the replay hot path — see #286).
        /// </summary>
        private List<ActiveEffect>? _activeEffects;

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

        public Battler(Player player)
            : this(player.GetAttributes(), player.SelectedSkills, player.Level)
        {
        }

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
        /// <b>stacks</b>: it adds its own <see cref="AttributeModifier"/> to the live collection and its own
        /// timed entry, so re-applying an already-active effect sums the magnitudes (additive amounts add,
        /// multiplicative factors compound) and each application expires on its own schedule. A new modifier
        /// may shift <see cref="MaxHealth"/>, so the health is re-clamped.
        /// </summary>
        public void ApplyEffect(SkillEffect effect, double amount)
        {
            var modifier = new AttributeModifier
            {
                Attribute = effect.AttributeId,
                Amount = amount,
                Type = effect.ModifierType,
                Source = EAttributeModifierSource.SkillEffect,
            };
            _attributes.AddModifier(modifier);
            (_activeEffects ??= []).Add(new ActiveEffect(modifier, _elapsedMs + effect.DurationMs));
            ClampHealthToMaxHealth();
        }

        /// <summary>
        /// Advances this battler's simulated-time clock by <paramref name="ms"/> and removes any active effect
        /// whose absolute expiry has been reached (its modifier removed and health re-clamped). Called at the
        /// start of each tick before any skill fires, so an effect influences exactly <c>DurationMs / tickSize</c>
        /// ticks counting the one it was applied on.
        /// </summary>
        public void AdvanceEffects(int ms)
        {
            // Advance the clock every tick, even with no active effects, so an effect applied on a later tick
            // still computes its absolute expiry from the correct elapsed time.
            _elapsedMs += ms;

            if (_activeEffects is null || _activeEffects.Count == 0)
            {
                return;
            }

            var removedAny = false;
            for (var i = _activeEffects.Count - 1; i >= 0; i--)
            {
                if (_activeEffects[i].ExpiresAtMs <= _elapsedMs)
                {
                    _attributes.RemoveModifier(_activeEffects[i].Modifier);
                    _activeEffects.RemoveAt(i);
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

        /// <summary>
        /// A timed skill effect active on a battler: the modifier it added to the collection (kept for
        /// identity removal on expiry) and the absolute simulated time (in <see cref="_elapsedMs"/> ms) at
        /// which it expires. Immutable — each application is its own entry, so stacking adds entries rather
        /// than mutating one and no per-tick state is rewritten.
        /// </summary>
        private sealed class ActiveEffect(AttributeModifier modifier, long expiresAtMs)
        {
            public AttributeModifier Modifier { get; } = modifier;
            public long ExpiresAtMs { get; } = expiresAtMs;
        }
    }
}
