using Game.Api.Models.Attributes;
using EnemyEntity = Game.Abstractions.Entities.Enemy;

namespace Game.Api.Models.Enemies
{
    public class Enemy : IModelFromSource<Enemy, EnemyEntity>
    {
        public int Id { get; set; }
        public required string Name { get; set; }
        public bool IsBoss { get; set; }
        public required IEnumerable<AttributeDistribution> AttributeDistribution { get; set; }
        public required IEnumerable<int> SkillPool { get; set; }
        public required IEnumerable<EnemySpawn> Spawns { get; set; }

        public static Enemy FromSource(EnemyEntity entity)
        {
            return new Enemy
            {
                AttributeDistribution = entity.AttributeDistributions.To().Model<AttributeDistribution>(),
                Name = entity.Name,
                Id = entity.Id,
                IsBoss = entity.IsBoss,
                SkillPool = entity.EnemySkills.Select(s => s.SkillId),
                Spawns = entity.ZoneEnemies.To().Model<EnemySpawn>(),
            };
        }
    }
}
