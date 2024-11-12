using Game.Core.Entities;
using static Game.Core.EAttribute;

namespace Game.Core.BattleSimulation
{
    /// <summary>
    /// The base class used to encapsulate a character for battle simulation.
    /// </summary>
    public abstract class Battler
    {
        /// <inheritdoc cref="BattleAttributes" />
        public abstract BattleAttributes Attributes { get; set; }

        /// <summary>
        /// The health for the battler for the current simulation.
        /// </summary>
        public abstract double CurrentHealth { get; set; }

        /// <summary>
        /// The skills that the battler will use during simulation.
        /// </summary>
        public abstract List<BattleSkill> Skills { get; set; }

        /// <summary>
        /// The current level of the battler.
        /// </summary>
        public abstract int Level { get; set; }

        /// <summary>
        /// True when the battler has been defeated in the simulation.
        /// </summary>
        public bool IsDead => CurrentHealth <= 0;

        /// <summary>
        /// Increases each <see cref="BattleSkill.ChargeTime"/> by the given <paramref name="timeDelta"/>. If any go over the <see cref="Skill.CooldownMs"/>
        /// then the <see cref="BattleSkill.ChargeTime"/> will be reset to 0 and the <see cref="BattleSkill"/> returned.
        /// </summary>
        /// <param name="timeDelta">The amount in milliseconds to advance each <see cref="BattleSkill.ChargeTime"/>.</param>
        /// <returns>A <see cref="List{T}"/> of each <see cref="BattleSkill"/> where the <see cref="BattleSkill.ChargeTime"/> reached the <see cref="Skill.CooldownMs"/>.</returns>
        public List<BattleSkill> AdvancedCooldowns(int timeDelta)
        {
            var firedSkills = new List<BattleSkill>();
            var cdMultiplier = 1 + (Attributes[CooldownRecovery] / 100);
            foreach (var skill in Skills)
            {
                skill.ChargeTime += timeDelta * cdMultiplier;
                if (skill.ChargeTime > skill.Data.CooldownMs)
                {
                    firedSkills.Add(skill);
                    skill.ChargeTime = 0;
                }
            }

            return firedSkills;
        }

        /// <summary>
        /// Decreases the <see cref="CurrentHealth"/> by the <paramref name="rawDamage"/> after reduction using the <see cref="Defense"/> of the <see cref="Battler"/>.
        /// </summary>
        /// <param name="rawDamage">The damage to apply before reductions.</param>
        public void TakeDamage(double rawDamage)
        {
            var damage = rawDamage - Attributes[Defense];
            damage = damage > 0 ? damage : 0;
            CurrentHealth -= damage;
        }
    }
}