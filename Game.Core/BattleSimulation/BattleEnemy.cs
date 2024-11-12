using Game.Core.Entities;
using static Game.Core.EAttribute;

namespace Game.Core.BattleSimulation
{
    public class BattleEnemy : Battler
    {
        public override BattleAttributes Attributes { get; set; }
        public override double CurrentHealth { get; set; }
        public override List<BattleSkill> Skills { get; set; }
        public override int Level { get; set; }

        public BattleEnemy(EnemyInstance enemyInstance, IEnumerable<Skill> enemySkills)
        {
            Level = enemyInstance.Level;
            Attributes = new BattleAttributes(enemyInstance.Attributes);
            CurrentHealth = Attributes[MaxHealth];
            Skills = enemySkills.Select(skill => new BattleSkill(skill)).ToList();
        }
    }
}
