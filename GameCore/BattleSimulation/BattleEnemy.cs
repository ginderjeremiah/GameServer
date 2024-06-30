using GameCore.Entities;
using static GameCore.EAttribute;

namespace GameCore.BattleSimulation
{
    public class BattleEnemy : Battler
    {
        public override BattleAttributes Attributes { get; set; }
        public override double CurrentHealth { get; set; }
        public override List<BattleSkill> Skills { get; set; }
        public override int Level { get; set; }

        public BattleEnemy(Enemy enemy, EnemyInstance enemyInstance)
        {
            var rng = new Random();
            var selectedSkills = enemy.EnemySkills.OrderBy(es => rng.Next()).Take(4).Select(es => es.Skill).ToList();
            var attributes = enemy.AttributeDistributions.Select(dist => new BattlerAttribute(dist, enemyInstance.Level)).ToList();
            Level = enemyInstance.Level;
            Attributes = new BattleAttributes(attributes);
            CurrentHealth = Attributes[MaxHealth];
            Skills = selectedSkills.Select(skill => new BattleSkill(skill)).ToList();
            enemyInstance.Attributes = attributes;
            enemyInstance.SelectedSkills = selectedSkills.Select(s => s.Id).ToList();
        }
    }
}
