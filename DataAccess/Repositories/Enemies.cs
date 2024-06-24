using GameCore;
using GameCore.DataAccess;
using GameCore.Entities;
using GameCore.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace DataAccess.Repositories
{
    internal class Enemies(IDatabaseService database) : BaseRepository(database), IEnemies
    {
        private static readonly SharedProbabilityTable zoneEnemiesTable = new();
        private static readonly object _lock = new();
        private static List<Enemy>? _enemyList;

        public async Task<IEnumerable<Enemy>> AllEnemiesAsync()
        {
            return _enemyList ??= await Database.Enemies
                .AsNoTracking()
                .Include(e => e.AttributeDistributions)
                .Include(e => e.EnemyDrops.Select(ed => ed.Item))
                .Include(e => e.EnemySkills.Select(es => es.Skill))
                .OrderBy(e => e.Id)
                .ToListAsync();
        }

        public async Task<Enemy?> GetEnemyAsync(int enemyId)
        {
            var enemies = (await AllEnemiesAsync()).ToList();
            if (enemies.Count >= enemyId)
            {
                return null;
            }
            else
            {
                return enemies[enemyId];
            }
        }

        public async Task<Enemy> GetRandomEnemyAsync(int zoneId)
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

            var enemies = (await AllEnemiesAsync()).ToList();

            return enemies[zoneEnemiesTable.GetRandomValue(zoneId)];
        }

        private (List<ProbabilityData>, List<AliasData>) GetProbabilitiesAndAliases(int zoneId)
        {
            var probabilities = Database.ZoneEnemyProbabilities
                .AsNoTracking()
                .Include(zep => zep.ZoneEnemy)
                .Where(zep => zep.ZoneEnemy.ZoneId == zoneId)
                .OrderBy(zep => zep.ZoneOrder)
                .Select(zep => new ProbabilityData { Alias = zep.ZoneEnemyId, Value = zep.ZoneEnemy.EnemyId, Probability = zep.Probability })
                .ToList();

            var aliases = Database.ZoneEnemyAliases
                .AsNoTracking()
                .Include(zea => zea.ZoneEnemy)
                .Where(zea => zea.ZoneEnemy.ZoneId == zoneId)
                .Select(zea => new AliasData { Alias = zea.ZoneEnemyId, Value = zea.ZoneEnemy.EnemyId })
                .ToList();

            return (probabilities, aliases);
        }
    }
}
