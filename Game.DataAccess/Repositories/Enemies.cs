using Game.Abstractions.DataAccess;
using Game.Infrastructure.Entities;
using Game.Core.Probability;
using Game.DataAccess.Mapping;
using Game.Infrastructure.Database;
using Contracts = Game.Abstractions.Contracts;
using CoreEnemy = Game.Core.Enemies.Enemy;

using Microsoft.EntityFrameworkCore;

namespace Game.DataAccess.Repositories
{
    internal class Enemies(GameContext context, ISkillEntityCache skills, IZones zones) : IEnemies, IEnemyEntityCache
    {
        private static List<Enemy>? _enemyList;
        // Per-zone enemy spawn tables keyed by zone id. Derived from the in-memory enemy list
        // (which eager-loads ZoneEnemies), so it is rebuilt whenever that list is (re)loaded.
        private static Dictionary<int, ProbabilityTable<int>>? _zoneEnemyTables;

        private readonly GameContext _context = context;
        private readonly ISkillEntityCache _skills = skills;
        private readonly IZones _zones = zones;

        public void InvalidateCache()
        {
            _enemyList = null;
            _zoneEnemyTables = null;
        }

        private List<Enemy> AllEntities(bool refreshCache = false)
        {
            if (_enemyList is null || refreshCache)
            {
                _enemyList = [.. _context.Enemies
                    .AsNoTracking()
                    .Include(e => e.AttributeDistributions)
                    .Include(e => e.EnemySkills)
                    .Include(e => e.ZoneEnemies)
                    .OrderBy(e => e.Id)];
                // Spawn tables are derived from the enemy list; drop them so they rebuild in step.
                _zoneEnemyTables = null;
            }

            return _enemyList;
        }

        public List<Contracts.Enemy> All(bool refreshCache = false)
        {
            return [.. AllEntities(refreshCache).Select(EnemyMapper.ToContract)];
        }

        public Enemy? GetEnemy(int enemyId)
        {
            var enemies = AllEntities();
            return enemies.Count <= enemyId ? null : enemies[enemyId];
        }

        public Enemy GetRandomEnemy(int zoneId)
        {
            if (!_zones.ValidateZoneId(zoneId))
            {
                throw new ArgumentOutOfRangeException(nameof(zoneId), zoneId, $"No zone exists with Id {zoneId}.");
            }

            var enemies = AllEntities();
            var zoneEnemyTables = _zoneEnemyTables ??= BuildZoneEnemyTables(enemies);

            if (!zoneEnemyTables.TryGetValue(zoneId, out var table))
            {
                throw new InvalidOperationException($"Zone {zoneId} has no enemies to spawn.");
            }

            return enemies[table.GetRandomValue()];
        }

        public CoreEnemy? GetDomainEnemy(int enemyId, int level)
        {
            var entity = GetEnemy(enemyId);
            return entity is null
                ? null
                : EnemyMapper.ToCore(entity, level, _skills.AllSkillEntities());
        }

        public CoreEnemy GetRandomDomainEnemy(int zoneId, int level)
        {
            var entity = GetRandomEnemy(zoneId);
            return EnemyMapper.ToCore(entity, level, _skills.AllSkillEntities());
        }

        /// <summary>
        /// Builds the per-zone enemy spawn tables from the in-memory enemy list. The ZoneEnemy weights are
        /// already eager-loaded by <see cref="AllEntities"/>, so no database query (and no lock) is required, and
        /// keying by zone id avoids the previous unbounded list growth for arbitrary or invalid ids.
        /// Retired enemies are excluded so they no longer roll as random encounters, while still resolving
        /// by id for existing references (e.g. an authored zone boss).
        /// </summary>
        private static Dictionary<int, ProbabilityTable<int>> BuildZoneEnemyTables(IEnumerable<Enemy> enemies)
        {
            return enemies
                .Where(enemy => enemy.RetiredAt is null)
                .SelectMany(enemy => enemy.ZoneEnemies)
                .GroupBy(zoneEnemy => zoneEnemy.ZoneId)
                .ToDictionary(
                    group => group.Key,
                    group => new ProbabilityTable<int>(
                        group.Select(zoneEnemy => new WeightedValue<int>(zoneEnemy.EnemyId, zoneEnemy.Weight))));
        }
    }
}
