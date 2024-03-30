using DataAccess.Models.Attributes;
using DataAccess.Models.Enemies;
using DataAccess.Models.Items;
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
                        var ds = GetProbabilitiesAndAliases(zoneId);
                        var probData = ds.Tables[0].To<ProbabilityData>();
                        var aliases = ds.Tables[1].AsEnumerable().Select(row => (row["Alias"].AsInt(), row["Value"].AsInt())).ToList();

                        zoneEnemiesTable.AddProbabilities(probData, zoneId);
                        zoneEnemiesTable.AddAliases(aliases);
                    }
                }
            }

            return AllEnemies()[zoneEnemiesTable.GetRandomValue(zoneId)];
        }

        private DataSet GetProbabilitiesAndAliases(int zoneId)
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

            return FillSet(commandText, new SqlParameter("@ZoneId", zoneId));
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


            var ds = FillSet(commandText);
            var attributes = ds.Tables[1].To<AttributeDistribution>()
                .GroupBy(att => att.EnemyId)
                .ToDictionary(g => g.Key, g => g.ToList());
            var drops = ds.Tables[2].To<ItemDrop>()
                .GroupBy(drop => drop.DroppedById)
                .ToDictionary(g => g.Key, g => g.ToList());
            var enemySkills = ds.Tables[3]
                .AsEnumerable()
                .GroupBy(row => row["EnemyId"].AsInt())
                .ToDictionary(g => g.Key, g => g.Select(row => row["SkillId"].AsInt()).ToList());

            return ds.Tables[0]
                .AsEnumerable()
                .Select(row =>
                {
                    var enemyId = row["EnemyId"].AsInt();
                    drops.TryGetValue(enemyId, out var dropList);
                    return new Enemy(enemyId, row["EnemyName"].AsString(), attributes[enemyId], dropList ?? new List<ItemDrop>(), enemySkills[enemyId]);
                })
                .OrderBy(enemy => enemy.EnemyId)
                .ToList();
        }
    }

    public interface IEnemies
    {
        public List<Enemy> AllEnemies();
        public Enemy GetEnemy(int enemyId);
        public Enemy GetRandomEnemy(int zoneId);
    }
}
