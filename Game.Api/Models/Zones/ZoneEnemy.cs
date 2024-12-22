using ZoneEnemyEntity = Game.Core.Entities.ZoneEnemy;

namespace Game.Api.Models.Zones
{
    public class ZoneEnemy : IModelFromSource<ZoneEnemy, ZoneEnemyEntity>
    {
        public int EnemyId { get; set; }
        public int Weight { get; set; }

        public static ZoneEnemy FromSource(ZoneEnemyEntity zoneEnemy)
        {
            return new ZoneEnemy
            {
                EnemyId = zoneEnemy.EnemyId,
                Weight = zoneEnemy.Weight
            };
        }
    }
}
