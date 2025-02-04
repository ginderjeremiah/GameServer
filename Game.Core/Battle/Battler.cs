using Game.Core.Attributes;
using Game.Core.Enemies;
using Game.Core.Inventories;
using Game.Core.Players;
using Game.Core.Skills;
using static Game.Core.EAttribute;

namespace Game.Core.Battle
{
    /// <summary>
    /// The base class used to encapsulate a character for battle simulation.
    /// </summary>
    public class Battler
    {
        /// <inheritdoc cref="AttributeCollection" />
        private readonly AttributeCollection _attributes;

        /// <summary>
        /// The health for the battler for the current simulation.
        /// </summary>
        public double CurrentHealth { get; private set; }

        /// <summary>
        /// The skills that the battler will use during simulation.
        /// </summary>
        public List<Skill> Skills { get; private set; }

        /// <summary>
        /// The current level of the battler.
        /// </summary>
        public int Level { get; private set; }

        /// <summary>
        /// True when the battler has been defeated in the simulation.
        /// </summary>
        public bool IsDead => CurrentHealth <= 0;

        /// <summary>
        /// Constructs a new <see cref="Battler"/> from a <see cref="Player"/> and their <see cref="Inventory"/>.
        /// </summary>
        /// <param name="player"></param>
        /// <param name="inventory"></param>
        public Battler(Player player, Inventory inventory)
            : this(new AttributeCollection(player.StatPoints.ToAttributeModifiers().Concat(inventory.GetEquippedAttributeModifiers())), player.Skills.Where(s => s.IsSelected), player.Level)
        {
        }

        /// <summary>
        /// Constructs a new <see cref="Battler"/> based on an <see cref="Enemy"/>.
        /// </summary>
        /// <param name="enemy"></param>
        public Battler(Enemy enemy)
            : this(new AttributeCollection(enemy.GetAttributeModifiers()), enemy.Skills.Where(s => s.IsSelected), enemy.Level)
        {
        }

        private Battler(AttributeCollection attributes, IEnumerable<Skill> skills, int level)
        {
            _attributes = attributes;
            CurrentHealth = _attributes[MaxHealth];
            Skills = skills.ToList();
            Level = level;
        }

        /// <summary>
        /// Updates the <see cref="Skill"/> cooldowns for the <see cref="Battler"/>.
        /// </summary>
        /// <param name="context"></param>
        public void Update(BattleContext context)
        {
            foreach (var skill in Skills)
            {
                skill.Update(context);
            }
        }

        /// <summary>
        /// Returns the cooldown multiplier for the battler.
        /// </summary>
        /// <returns></returns>
        public double GetCooldownMultiplier()
        {
            return 1 + (_attributes[CooldownRecovery] / 100);
        }

        /// <summary>
        /// Returns the value of the <see cref="EAttribute"/> for the <see cref="Battler"/>.
        /// </summary>
        /// <param name="attribute"></param>
        /// <returns></returns>
        public double GetAttributeValue(EAttribute attribute)
        {
            return _attributes[attribute];
        }

        /// <summary>
        /// Decreases the <see cref="CurrentHealth"/> by the <paramref name="rawDamage"/> after reduction using the <see cref="Defense"/> of the <see cref="Battler"/>.
        /// </summary>
        /// <param name="rawDamage">The damage to apply before reductions.</param>
        public void TakeDamage(double rawDamage)
        {
            var damage = rawDamage - _attributes[Defense];
            damage = damage > 0 ? damage : 0;
            CurrentHealth -= damage;
        }
    }
}