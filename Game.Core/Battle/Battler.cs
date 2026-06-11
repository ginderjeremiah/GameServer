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
            return 1 + (_attributes[CooldownRecovery] / 100);
        }

        public double GetAttributeValue(EAttribute attribute)
        {
            return _attributes[attribute];
        }

        public double TakeDamage(double rawDamage)
        {
            var damage = rawDamage - _attributes[Defense];
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
        /// Applies <paramref name="effect"/> as a timed attribute modifier on this battler. Re-applying an
        /// already-active effect (matched by its authored id) refreshes its remaining duration to full
        /// without adding a second modifier (no stacking); a new effect adds a <see cref="AttributeModifier"/>
        /// to the live collection and may shift <see cref="MaxHealth"/>, so the health is re-clamped.
        /// </summary>
        public void ApplyEffect(SkillEffect effect)
        {
            if (_activeEffects is not null)
            {
                foreach (var active in _activeEffects)
                {
                    if (active.Source.Id == effect.Id)
                    {
                        active.RemainingMs = effect.DurationMs;
                        return;
                    }
                }
            }

            var modifier = new AttributeModifier
            {
                Attribute = effect.AttributeId,
                Amount = effect.Amount,
                Type = effect.ModifierType,
                Source = EAttributeModifierSource.SkillEffect,
            };
            _attributes.AddModifier(modifier);
            (_activeEffects ??= []).Add(new ActiveEffect(effect, modifier, effect.DurationMs));
            ClampHealthToMaxHealth();
        }

        /// <summary>
        /// Advances every active effect by <paramref name="ms"/>, removing any whose remaining duration has
        /// elapsed (its modifier is removed and the affected caches invalidated). Called at the start of each
        /// tick before any skill fires, so an effect influences exactly <c>DurationMs / tickSize</c> ticks
        /// counting the one it was applied on.
        /// </summary>
        public void AdvanceEffects(int ms)
        {
            if (_activeEffects is null || _activeEffects.Count == 0)
            {
                return;
            }

            var removedAny = false;
            for (var i = _activeEffects.Count - 1; i >= 0; i--)
            {
                var active = _activeEffects[i];
                active.RemainingMs -= ms;
                if (active.RemainingMs <= 0)
                {
                    _attributes.RemoveModifier(active.Modifier);
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
        /// A timed skill effect active on a battler: the authored <see cref="SkillEffect"/> it came from, the
        /// modifier it added to the collection (kept for identity removal on expiry), and the remaining
        /// duration in ms.
        /// </summary>
        private sealed class ActiveEffect(SkillEffect source, AttributeModifier modifier, int remainingMs)
        {
            public SkillEffect Source { get; } = source;
            public AttributeModifier Modifier { get; } = modifier;
            public int RemainingMs { get; set; } = remainingMs;
        }
    }
}
