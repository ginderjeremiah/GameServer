using DataAccess.Models.Enemies;
using DataAccess.Models.Items;
using DataAccess.Models.Stats;
using GameLibrary;
using System.Data;
using System.Data.SqlClient;

namespace DataAccess.Repositories
{
    internal class Enemies : BaseRepository, IEnemies
    {

        public Enemies(string connectionString) : base(connectionString) { }

        public DataSet GetProbabilitiesAndAliases(int zoneId)
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

        public List<Enemy> GetAllEnemyData()
        {
            var commandText = @"
                SELECT
	                E.EnemyId,
	                E.EnemyName,
                    EBSD.StrengthWeight,
                    EBSD.EnduranceWeight,
                    EBSD.IntellectWeight,
                    EBSD.AgilityWeight,
                    EBSD.DexterityWeight,
                    EBSD.LuckWeight,
                    EBSD.BaseStats,
                    EBSD.StatsPerLevel
                FROM Enemies AS E
                INNER JOIN EnemyBaseStatDistributions AS EBSD
                ON E.EnemyId = EBSD.EnemyId
                ORDER BY E.EnemyId

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
            var enemySkills = ds.Tables[2]
                .AsEnumerable()
                .GroupBy(row => row["EnemyId"].AsInt())
                .ToDictionary(g => g.Key, g => g.Select(row => row["SkillId"].AsInt()).ToList());
            var drops = ds.Tables[1].To<ItemDrop>()
                .GroupBy(drop => drop.DroppedById)
                .ToDictionary(g => g.Key, g => g.ToList());
            return ds.Tables[0]
                .AsEnumerable()
                .Select(row =>
                {
                    var enemyId = row["EnemyId"].AsInt();
                    var statDist = row.To<BaseStatDistribution>();
                    drops.TryGetValue(enemyId, out var dropList);
                    return new Enemy(enemyId, row["EnemyName"].AsString(), statDist, dropList ?? new List<ItemDrop>(), enemySkills[enemyId]);
                })
                .OrderBy(enemy => enemy.EnemyId)
                .ToList();
        }
    }

    public interface IEnemies
    {
        public DataSet GetProbabilitiesAndAliases(int zoneId);
        public List<Enemy> GetAllEnemyData();
    }
}
