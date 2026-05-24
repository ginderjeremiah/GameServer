using Game.Api.Models.Attributes;
using EnemyEntity = Game.Abstractions.Entities.Enemy;

namespace Game.Api.Models.Enemies
{
    public class Enemy : IModelFromSource<Enemy, EnemyEntity>
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public IEnumerable<AttributeDistribution> AttributeDistribution { get; set; }
        public IEnumerable<int> SkillPool { get; set; }

        public static Enemy FromSource(EnemyEntity entity)
        {
            return new Enemy
            {
                AttributeDistribution = entity.AttributeDistributions.To().Model<AttributeDistribution>(),
                Name = entity.Name,
                Id = entity.Id,
                SkillPool = entity.EnemySkills.Select(s => s.SkillId),
            };
        }
    }
}
