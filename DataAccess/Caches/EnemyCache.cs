using DataAccess.Models.Enemies;
using GameLibrary;
using System.Data;

namespace DataAccess.Caches
{
    internal class EnemyCache : IEnemyCache
    {
        private static EnemyCache? _instance;
        private readonly SharedProbabilityTable zoneEnemiesTable = new();
        private readonly object _lock = new();
        private readonly IRepositoryManager _repositoryManager;
        private readonly List<Enemy> _enemyList;

        public EnemyCache(IRepositoryManager repositoryManager)
        {
            if (_instance is not null)
                throw new Exception("Enemy Cache already instantiated!");

            _instance = this;
            _repositoryManager = repositoryManager;
            _enemyList = _repositoryManager.Enemies.GetAllEnemyData();
        }

        public List<Enemy> AllEnemies()
        {
            return _enemyList;
        }

        public Enemy GetEnemy(int enemyId)
        {
            return _enemyList[enemyId];
        }

        public Enemy GetRandomEnemy(int zoneId)
        {
            if (!zoneEnemiesTable.HasProbabilities(zoneId))
            {
                lock (_lock)
                {
                    if (!zoneEnemiesTable.HasProbabilities(zoneId))
                    {
                        var ds = _repositoryManager.Enemies.GetProbabilitiesAndAliases(zoneId);
                        var probData = ds.Tables[0].To<ProbabilityData>();
                        var aliases = ds.Tables[1].AsEnumerable().Select(row => (row["Alias"].AsInt(), row["Value"].AsInt())).ToList();

                        zoneEnemiesTable.AddProbabilities(probData, zoneId);
                        zoneEnemiesTable.AddAliases(aliases);
                    }
                }
            }

            return _enemyList[zoneEnemiesTable.GetRandomValue(zoneId)];
        }
    }

    public interface IEnemyCache
    {
        public List<Enemy> AllEnemies();
        public Enemy GetEnemy(int enemyId);
        public Enemy GetRandomEnemy(int zoneId);
    }
}
