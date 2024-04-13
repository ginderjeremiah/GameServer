using DataAccess.Entities.Skills;
using GameServer.Models.Attributes;
using GameServer.Models.Enemies;
using static GameServer.AttributeType;

namespace GameServer.BattleSimulation
{
    public class BattleEnemy : Battler
    {
        public override BattleAttributes Attributes { get; set; }
        public override double CurrentHealth { get; set; }
        public override List<BattleSkill> Skills { get; set; }
        public override int Level { get; set; }

        public BattleEnemy(DataAccess.Entities.Enemies.Enemy enemy, EnemyInstance enemyInstance, List<Skill> allSkills)
        {
            var rng = new Random();
            var selectedSkills = enemy.SkillPool.OrderBy(s => rng.Next()).Take(4).ToList();
            var attributes = enemy.AttributeDistribution.Select(dist => new BattlerAttribute(dist, enemyInstance.Level)).ToList();
            Level = enemyInstance.Level;
            Attributes = new BattleAttributes(attributes);
            CurrentHealth = Attributes[MaxHealth];
            Skills = selectedSkills.Select(id => new BattleSkill(allSkills[id])).ToList();
            enemyInstance.Attributes = attributes;
            enemyInstance.SelectedSkills = selectedSkills;
        }
    }
}
