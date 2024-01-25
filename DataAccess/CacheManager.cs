using DataAccess.Caches;

namespace DataAccess
{
    public class CacheManager : ICacheManager
    {
        private readonly EnemyCache _enemyCache;
        private readonly ZoneCache _zoneCache;
        private readonly SkillCache _skillCache;
        private readonly LootCache _lootCache;

        public IEnemyCache EnemyCache => _enemyCache;
        public IZoneCache ZoneCache => _zoneCache;
        public ISkillCache SkillCache => _skillCache;
        public ILootCache LootCache => _lootCache;

        public CacheManager(string connectionString)
        {
            var repositoryManager = new RepositoryManager(connectionString);
            _enemyCache = new EnemyCache(repositoryManager);
            _zoneCache = new ZoneCache(repositoryManager);
            _skillCache = new SkillCache(repositoryManager);
            _lootCache = new LootCache(repositoryManager, EnemyCache, ZoneCache);
        }
    }

    public interface ICacheManager
    {
        public IEnemyCache EnemyCache { get; }
        public IZoneCache ZoneCache { get; }
        public ISkillCache SkillCache { get; }
        public ILootCache LootCache { get; }
    }
}