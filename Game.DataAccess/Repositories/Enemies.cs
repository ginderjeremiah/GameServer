using Game.Abstractions.DataAccess;
using Game.DataAccess.Mapping;
using Game.DataAccess.Repositories.Caching;
using Game.Infrastructure.Entities;
using Contracts = Game.Abstractions.Contracts;
using CoreEnemy = Game.Core.Enemies.Enemy;

namespace Game.DataAccess.Repositories
{
    internal class Enemies(EnemiesCacheHolder holder, ISkillEntityCache skills, IZones zones) : IEnemies, IEnemyEntityCache
    {
        private EnemySnapshot Snapshot => holder.Current;

        public List<Contracts.Enemy> All()
        {
            return [.. Snapshot.Enemies.Select(EnemyMapper.ToContract)];
        }

        public Enemy? GetEnemy(int enemyId)
        {
            return Snapshot.Enemies.Lookup(enemyId);
        }

        public Enemy GetRandomEnemy(int zoneId)
        {
            if (!zones.ValidateZoneId(zoneId))
            {
                throw new ArgumentOutOfRangeException(nameof(zoneId), zoneId, $"No zone exists with Id {zoneId}.");
            }

            var snapshot = Snapshot;
            if (!snapshot.ZoneEnemyTables.TryGetValue(zoneId, out var table))
            {
                throw new InvalidOperationException($"Zone {zoneId} has no enemies to spawn.");
            }

            return snapshot.Enemies[table.GetRandomValue()];
        }

        public CoreEnemy? GetDomainEnemy(int enemyId, int level)
        {
            var entity = GetEnemy(enemyId);
            return entity is null
                ? null
                : EnemyMapper.ToCore(entity, level, skills.AllSkillEntities());
        }

        public CoreEnemy GetRandomDomainEnemy(int zoneId, int level)
        {
            var entity = GetRandomEnemy(zoneId);
            return EnemyMapper.ToCore(entity, level, skills.AllSkillEntities());
        }
    }
}
