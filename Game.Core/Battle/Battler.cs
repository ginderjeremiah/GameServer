using Game.Core.Attributes;
using Game.Core.Enemies;
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

        public double CurrentHealth { get; private set; }

        public List<BattleSkill> Skills { get; private set; }

        public int Level { get; private set; }

        public bool IsDead => CurrentHealth <= 0;

        public Battler(Player player)
            : this(player.GetAttributes(), player.SelectedSkills, player.Level)
        {
        }

        public Battler(Enemy enemy)
            : this(new AttributeCollection(enemy.GetAttributeModifiers()), enemy.Skills, enemy.Level)
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
    }
}