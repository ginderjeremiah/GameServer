using Game.Abstractions.DataAccess;
using Game.Abstractions.Entities;
using Game.Core.Probability;
using Game.DataAccess.Mapping;
using Game.Infrastructure.Database;
using CoreEnemy = Game.Core.Enemies.Enemy;

using Microsoft.EntityFrameworkCore;

namespace Game.DataAccess.Repositories
{
    internal class Enemies(GameContext context, ISkills skills) : IEnemies
    {
        private static readonly List<ProbabilityTable<int>?> zoneEnemiesTables = [];
        private static readonly object _lock = new();
        private static List<Enemy>? _enemyList;

        private readonly GameContext _context = context;
        private readonly ISkills _skills = skills;

        public void InvalidateCache()
        {
            _enemyList = null;
            lock (_lock) { zoneEnemiesTables.Clear(); }
        }

        public List<Enemy> All(bool refreshCache = false)
        {
            if (_enemyList is null || refreshCache)
            {
                _enemyList = [.. _context.Enemies
                    .AsNoTracking()
                    .Include(e => e.AttributeDistributions)
                    .Include(e => e.EnemySkills)
                    .Include(e => e.ZoneEnemies)
                    .OrderBy(e => e.Id)];
            }

            return _enemyList;
        }

        public Enemy? GetEnemy(int enemyId)
        {
            var enemies = All();
            return enemies.Count <= enemyId ? null : enemies[enemyId];
        }

        public Enemy GetRandomEnemy(int zoneId)
        {
            var enemies = All();

            if (zoneEnemiesTables.Count > zoneId)
            {
                var table = zoneEnemiesTables[zoneId];
                if (table is not null)
                {
                    return enemies[table.GetRandomValue()];
                }
            }

            //TODO: Refactor to use async queries and also not require a lock
            lock (_lock)
            {
                //TODO: Make this not allow adding an insane amount of values to the list if someone passes in a big id.
                for (int i = zoneEnemiesTables.Count - 1; i < zoneId; i++)
                {
                    zoneEnemiesTables.Add(null);
                }

                var table = zoneEnemiesTables[zoneId];
                if (table is null)
                {
                    var weightedZoneEnemies = _context.ZoneEnemies
                        .Where(ze => ze.ZoneId == zoneId)
                        .Select(ze => new WeightedValue<int>(ze.EnemyId, ze.Weight));

                    table = new ProbabilityTable<int>(weightedZoneEnemies);
                    zoneEnemiesTables[zoneId] = table;
                }

                return enemies[table.GetRandomValue()];
            }
        }

        public CoreEnemy? GetDomainEnemy(int enemyId, int level)
        {
            var entity = GetEnemy(enemyId);
            return entity is null
                ? null
                : EnemyMapper.ToCore(entity, level, _skills.AllSkills());
        }

        public CoreEnemy GetRandomDomainEnemy(int zoneId, int level)
        {
            var entity = GetRandomEnemy(zoneId);
            return EnemyMapper.ToCore(entity, level, _skills.AllSkills());
        }
    }
}
