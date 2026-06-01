using ZoneEnemyEntity = Game.Abstractions.Entities.ZoneEnemy;

namespace Game.Api.Models.Enemies
{
    public class EnemySpawn : IModelFromSource<EnemySpawn, ZoneEnemyEntity>
    {
        public int ZoneId { get; set; }
        public int Weight { get; set; }

        public static EnemySpawn FromSource(ZoneEnemyEntity zoneEnemy)
        {
            return new EnemySpawn
            {
                ZoneId = zoneEnemy.ZoneId,
                Weight = zoneEnemy.Weight
            };
        }
    }
}
