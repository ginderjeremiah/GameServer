using DataAccess.Entities.Enemies;
using DataAccess.Repositories;

namespace GameTests.Mocks.DataAccess.Repositories
{
    internal class MockEnemies : IEnemies
    {
        public List<Enemy> AllEnemies()
        {
            throw new NotImplementedException();
        }

        public Enemy GetEnemy(int enemyId)
        {
            throw new NotImplementedException();
        }

        public Enemy GetRandomEnemy(int zoneId)
        {
            throw new NotImplementedException();
        }
    }
}
