using DataAccess.Models.Enemies;
using DataAccess.Models.PlayerAttributes;
using DataAccess.Models.Skills;
using GameServer.Models.Common;
using static DataAccess.Attributes;

namespace GameServer.BattleSimulation
{
    public class BattleEnemy : Battler
    {
        public override BattleAttributes Attributes { get; set; }
        public override double CurrentHealth { get; set; }
        public override List<BattleSkill> Skills { get; set; }
        public override int Level { get; set; }
        public BattleEnemy(Enemy enemy, EnemyInstance enemyInstance, List<Skill> allSkills)
        {
            Level = enemyInstance.EnemyLevel;
            var attributes = enemy.AttributeDistribution.Select(dist => new PlayerAttribute
            {
                AttributeId = dist.AttributeId,
                Amount = dist.BaseAmount + dist.AmountPerLevel * enemyInstance.EnemyLevel
            }).ToList();
            Attributes = new BattleAttributes(attributes);
            enemyInstance.Attributes = attributes;
            CurrentHealth = Attributes[MaxHealth];
            Skills = enemy.SelectedSkills.Select(id => new BattleSkill(allSkills[id])).ToList();
        }
    }
}
