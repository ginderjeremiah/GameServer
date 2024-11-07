using Game.Core;
using Game.Core.DataAccess;
using Game.Core.Entities;
using Game.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace Game.DataAccess.Repositories
{
    internal class Enemies(GameContext context) : IEnemies
    {
        private static readonly SharedProbabilityTable zoneEnemiesTable = new();
        private static readonly object _lock = new();
        private static List<Enemy>? _enemyList;

        private readonly GameContext _context = context;

        public List<Enemy> AllEnemies(bool refreshCache = false)
        {
            if (_enemyList is null || refreshCache)
            {
                _enemyList ??= [.. _context.Enemies
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
            var enemies = AllEnemies();
            return enemies.Count <= enemyId ? null : enemies[enemyId];
        }

        public Enemy GetRandomEnemy(int zoneId)
        {
            if (!zoneEnemiesTable.HasProbabilities(zoneId))
            {
                lock (_lock)
                {
                    if (!zoneEnemiesTable.HasProbabilities(zoneId))
                    {
                        var data = GetProbabilitiesAndAliases(zoneId);
                        var probData = data.Item1;
                        var aliases = data.Item2;

                        zoneEnemiesTable.AddProbabilities(probData, zoneId);
                        zoneEnemiesTable.AddAliases(aliases);
                    }
                }
            }

            var enemies = AllEnemies();

            return enemies[zoneEnemiesTable.GetRandomValue(zoneId)];
        }

        private (List<ProbabilityData>, List<AliasData>) GetProbabilitiesAndAliases(int zoneId)
        {
            var probabilities = _context.ZoneEnemyProbabilities
                .AsNoTracking()
                .Include(zep => zep.ZoneEnemy)
                .Where(zep => zep.ZoneEnemy.ZoneId == zoneId)
                .OrderBy(zep => zep.ZoneOrder)
                .Select(zep => new ProbabilityData { Alias = zep.ZoneEnemyId, Value = zep.ZoneEnemy.EnemyId, Probability = zep.Probability })
                .ToList();

            var aliases = _context.ZoneEnemyAliases
                .AsNoTracking()
                .Include(zea => zea.ZoneEnemy)
                .Where(zea => zea.ZoneEnemy.ZoneId == zoneId)
                .Select(zea => new AliasData { Alias = zea.ZoneEnemyId, Value = zea.ZoneEnemy.EnemyId })
                .ToList();

            return (probabilities, aliases);
        }
    }
}
