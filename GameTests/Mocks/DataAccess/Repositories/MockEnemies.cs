using GameCore.DataAccess;
using GameCore.Entities;

namespace GameTests.Mocks.DataAccess.Repositories
{
    internal class MockEnemies : IEnemies
    {
        public List<Enemy> Enemies { get; set; } = new();
        public List<Enemy> AllEnemies()
        {
            return Enemies;
        }

        public Enemy GetEnemy(int enemyId)
        {
            return Enemies.First(enemy => enemy.EnemyId == enemyId);
        }

        public Enemy GetRandomEnemy(int zoneId)
        {
            return Enemies.First();
        }
    }
}
