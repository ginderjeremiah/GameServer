using Game.Core.DataAccess;
using Game.Core.Entities;
using Game.Core.Probability;
using Game.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace Game.DataAccess.Repositories
{
    internal class Enemies(GameContext context) : IEnemies
    {
        private static readonly List<ProbabilityTable<int>?> zoneEnemiesTables = [];
        private static readonly object _lock = new();
        private static List<Enemy>? _enemyList;

        private readonly GameContext _context = context;

        public List<Enemy> All(bool refreshCache = false)
        {
            if (_enemyList is null || refreshCache)
            {
                _enemyList = [.. _context.Enemies
                    .AsNoTracking()
                    .Include(e => e.AttributeDistributions)
                    .Include(e => e.EnemyDrops)
                    .Include(e => e.EnemySkills)
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
    }
}
