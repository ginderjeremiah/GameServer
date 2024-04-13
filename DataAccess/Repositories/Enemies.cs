using DataAccess.Entities.Drops;
using DataAccess.Entities.Enemies;
using GameLibrary;
using System.Data;
using System.Data.SqlClient;

namespace DataAccess.Repositories
{
    internal class Enemies : BaseRepository, IEnemies
    {
        private static readonly SharedProbabilityTable zoneEnemiesTable = new();
        private static readonly object _lock = new();
        private static List<Enemy>? _enemyList;

        public Enemies(string connectionString) : base(connectionString) { }

        public List<Enemy> AllEnemies()
        {
            return _enemyList ??= GetAllEnemyData();
        }

        public Enemy GetEnemy(int enemyId)
        {
            return AllEnemies()[enemyId];
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

            return AllEnemies()[zoneEnemiesTable.GetRandomValue(zoneId)];
        }

        private (List<ZoneEnemyProbability>, List<ZoneEnemyAlias>) GetProbabilitiesAndAliases(int zoneId)
        {
            var commandText = @"
                SELECT
                    ZEP.Probability,
                    ZEP.ZoneEnemyId AS Alias,
                    ZE.EnemyId AS Value
                FROM ZoneEnemiesProbabilities AS ZEP
                INNER JOIN ZoneEnemies AS ZE
                ON ZEP.ZoneEnemyId = ZE.ZoneEnemyId
                WHERE ZE.ZoneId = @ZoneId
                ORDER BY ZEP.ZoneOrder

                SELECT
                    ZEA.ZoneEnemyId AS Alias,
                    ZE.EnemyId AS Value
                FROM ZoneEnemiesAliases AS ZEA
                INNER JOIN ZoneEnemies AS ZE
                ON ZEA.ZoneEnemyIdAlias = ZE.ZoneEnemyId
                WHERE ZE.ZoneId = @ZoneId";

            return QueryToList<ZoneEnemyProbability, ZoneEnemyAlias>(commandText, new SqlParameter("@ZoneId", zoneId));
        }

        private List<Enemy> GetAllEnemyData()
        {
            var commandText = @"
                SELECT
	                EnemyId,
	                EnemyName
                FROM Enemies
                ORDER BY EnemyId

                SELECT
                    EnemyId,
                    AttributeId,
                    BaseAmount,
                    AmountPerLevel
                FROM EnemyAttributeDistributions

                SELECT
	                EnemyId AS DroppedById,
	                ItemId,
	                DropRate
                FROM EnemyDrops

                SELECT
                    EnemyId,
                    SkillId
                FROM EnemySkills";

            var result = QueryToList<Enemy, AttributeDistribution, ItemDrop, EnemySkill>(commandText);

            var attributes = result.Item2
                .GroupBy(att => att.EnemyId)
                .ToDictionary(g => g.Key, g => g.ToList());
            var drops = result.Item3
                .GroupBy(drop => drop.DroppedById)
                .ToDictionary(g => g.Key, g => g.ToList());
            var enemySkills = result.Item4
                .AsEnumerable()
                .GroupBy(enemySkill => enemySkill.EnemyId)
                .ToDictionary(g => g.Key, g => g.Select(enemySkill => enemySkill.SkillId).ToList());

            foreach (var enemy in result.Item1)
            {
                var id = enemy.EnemyId;
                drops.TryGetValue(id, out var dropList);
                enemy.AttributeDistribution = attributes[id];
                enemy.EnemyDrops = dropList ?? new List<ItemDrop>();
                enemy.SkillPool = enemySkills[id];
            }

            return result.Item1;
        }
    }

    public interface IEnemies
    {
        public List<Enemy> AllEnemies();
        public Enemy GetEnemy(int enemyId);
        public Enemy GetRandomEnemy(int zoneId);
    }
}
