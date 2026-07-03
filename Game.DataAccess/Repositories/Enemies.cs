using Game.Abstractions.DataAccess;
using Game.DataAccess.Mapping;
using Game.DataAccess.Repositories.Caching;
using Game.Infrastructure.Entities;
using Contracts = Game.Abstractions.Contracts;
using CoreEnemy = Game.Core.Enemies.Enemy;

namespace Game.DataAccess.Repositories
{
    internal class Enemies(EnemiesCacheHolder holder, IZones zones) : IEnemies, IEnemyEntityCache
    {
        private EnemySnapshot Snapshot => holder.Current;

        // The snapshot instance changes on every build-then-swap, so it doubles as the content-version key.
        public object VersionKey => Snapshot;

        public List<Contracts.Enemy> All()
        {
            return [.. Snapshot.Enemies.Select(EnemyMapper.ToContract)];
        }

        public Enemy? GetEnemy(int enemyId)
        {
            return Snapshot.Enemies.Lookup(enemyId);
        }

        public CoreEnemy? GetDomainEnemy(int enemyId, int level)
        {
            // Clones the snapshot's pre-mapped, level-independent template rather than re-mapping the enemy's
            // skill graph per call; the level (and the encounter's battle-skill selection) is applied per clone.
            return Snapshot.Templates.Lookup(enemyId)?.ToEnemy(level);
        }

        public CoreEnemy GetRandomDomainEnemy(int zoneId, int level)
        {
            var snapshot = Snapshot;
            return snapshot.Templates[GetRandomEnemyId(snapshot, zoneId)].ToEnemy(level);
        }

        public bool HasSpawnableEnemies(int zoneId)
        {
            return Snapshot.ZoneEnemyTables.ContainsKey(zoneId);
        }

        // Resolves a random enemy id from the requested zone's spawn table against the captured snapshot,
        // validating the zone and that it has a spawn table. Shared by the entity and domain random reads so
        // both apply the same zone checks and read a single consistent snapshot.
        private int GetRandomEnemyId(EnemySnapshot snapshot, int zoneId)
        {
            if (!zones.ValidateZoneId(zoneId))
            {
                throw new ArgumentOutOfRangeException(nameof(zoneId), zoneId, $"No zone exists with Id {zoneId}.");
            }

            if (!snapshot.ZoneEnemyTables.TryGetValue(zoneId, out var table))
            {
                throw new InvalidOperationException($"Zone {zoneId} has no enemies to spawn.");
            }

            return table.GetRandomValue();
        }
    }
}
