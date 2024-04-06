using GameServer.Models.Attributes;
using GameServer.Models.Items;

namespace GameServer.Models.Enemies
{
    public class Enemy : IModel
    {
        public List<ItemDrop> EnemyDrops { get; set; }
        public List<AttributeDistribution> AttributeDistribution { get; set; }
        public string EnemyName { get; set; }
        public int EnemyId { get; set; }
        public List<int> SelectedSkills { get; set; }

        public Enemy(DataAccess.Models.Enemies.Enemy enemy)
        {
            EnemyDrops = enemy.EnemyDrops.Select(drop => new ItemDrop(drop)).ToList();
            AttributeDistribution = enemy.AttributeDistribution.Select(dist => new AttributeDistribution(dist)).ToList();
            EnemyName = enemy.EnemyName;
            EnemyId = enemy.EnemyId;
            SelectedSkills = enemy.SelectedSkills;
        }
    }
}
