using GameCore.Entities.Enemies;

namespace GameCore.DataAccess
{
    public interface IEnemies
    {
        public List<Enemy> AllEnemies();
        public Enemy GetEnemy(int enemyId);
        public Enemy GetRandomEnemy(int zoneId);
    }
}
