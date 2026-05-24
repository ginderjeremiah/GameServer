using Game.Abstractions.DataAccess;
using Game.Abstractions.Entities;
using CoreEnemy = Game.Core.Enemies.Enemy;

namespace Game.Application.Tests.Fakes
{
    /// <summary>
    /// In-memory fake for <see cref="IWorldRepository"/> used in unit tests.
    /// </summary>
    internal class FakeWorldRepository : IWorldRepository
    {
        private readonly FakeEnemies _enemies;

        public FakeWorldRepository(CoreEnemy? domainEnemy = null)
        {
            _enemies = new FakeEnemies(domainEnemy);
        }

        public IEnemies Enemies => _enemies;
        public IZones Zones => throw new NotSupportedException("Zones not needed in these tests.");
    }

    internal class FakeEnemies(CoreEnemy? domainEnemy) : IEnemies
    {
        private readonly CoreEnemy? _domainEnemy = domainEnemy;

        public List<Enemy> All(bool refreshCache = false) => [];
        public Enemy? GetEnemy(int enemyId) => null;
        public Enemy GetRandomEnemy(int zoneId) => throw new NotSupportedException();

        public CoreEnemy? GetDomainEnemy(int enemyId, int level) => _domainEnemy;
        public CoreEnemy GetRandomDomainEnemy(int zoneId, int level)
            => _domainEnemy ?? throw new InvalidOperationException("No domain enemy configured.");
    }
}
