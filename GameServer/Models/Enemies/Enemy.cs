using GameServer.Models.Attributes;
using GameServer.Models.Items;

namespace GameServer.Models.Enemies
{
    public class Enemy : IModel
    {
        public int EnemyId { get; set; }
        public string Name { get; set; }
        public List<ItemDrop> Drops { get; set; }
        public List<AttributeDistribution> AttributeDistribution { get; set; }
        public List<int> SkillPool { get; set; }

        public Enemy() { }

        public Enemy(GameCore.Entities.Enemy enemy)
        {
            Drops = enemy.EnemyDrops.Select(drop => new ItemDrop(drop)).ToList();
            AttributeDistribution = enemy.AttributeDistributions.Select(dist => new AttributeDistribution(dist)).ToList();
            Name = enemy.Name;
            EnemyId = enemy.Id;
            SkillPool = enemy.EnemySkills.Select(s => s.SkillId).ToList();
        }
    }
}
