using Game.Api;
using Game.Api.Models.Attributes;
using Game.Api.Models.Items;
using EnemyEntity = Game.Core.Entities.Enemy;

namespace Game.Api.Models.Enemies
{
    public class Enemy : IModelFromSource<Enemy, EnemyEntity>
    {
        public int EnemyId { get; set; }
        public string Name { get; set; }
        public IEnumerable<ItemDrop> Drops { get; set; }
        public IEnumerable<AttributeDistribution> AttributeDistribution { get; set; }
        public IEnumerable<int> SkillPool { get; set; }

        public static Enemy FromSource(EnemyEntity entity)
        {
            return new Enemy
            {
                Drops = entity.EnemyDrops.To().Model<ItemDrop>(),
                AttributeDistribution = entity.AttributeDistributions.To().Model<AttributeDistribution>(),
                Name = entity.Name,
                EnemyId = entity.Id,
                SkillPool = entity.EnemySkills.Select(s => s.SkillId),
            };
        }
    }
}
