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
        private readonly FakeZones _zones;

        public FakeWorldRepository(CoreEnemy? domainEnemy = null, Zone? zone = null)
        {
            _enemies = new FakeEnemies(domainEnemy);
            _zones = new FakeZones(zone);
        }

        public IEnemies Enemies => _enemies;
        public IZones Zones => _zones;
    }

    internal class FakeEnemies(CoreEnemy? domainEnemy) : IEnemies
    {
        private readonly CoreEnemy? _domainEnemy = domainEnemy;

        public void InvalidateCache() { }
        public List<Enemy> All(bool refreshCache = false) => [];
        public Enemy? GetEnemy(int enemyId) => null;
        public Enemy GetRandomEnemy(int zoneId) => throw new NotSupportedException();

        public CoreEnemy? GetDomainEnemy(int enemyId, int level) => _domainEnemy;
        public CoreEnemy GetRandomDomainEnemy(int zoneId, int level)
            => _domainEnemy ?? throw new InvalidOperationException("No domain enemy configured.");
    }

    internal class FakeZones(Zone? zone) : IZones
    {
        private readonly Zone? _zone = zone;

        public void InvalidateCache() { }
        public List<Zone> All(bool refreshCache = false) => _zone is not null ? [_zone] : [];
        public Zone? GetZone(int zoneId) => _zone?.Id == zoneId ? _zone : null;
        public bool ValidateZoneId(int zoneId) => _zone?.Id == zoneId;
        public async IAsyncEnumerable<ZoneEnemy> ZoneEnemies(int zoneId)
        {
            await Task.CompletedTask;
            yield break;
        }
    }
}
