using GameCore.Entities;

namespace GameCore.DataAccess
{
    public interface IEnemies
    {
        public List<Enemy> AllEnemies(bool refreshCache = false);
        public Enemy? GetEnemy(int enemyId);
        public Enemy GetRandomEnemy(int zoneId);
    }
}
