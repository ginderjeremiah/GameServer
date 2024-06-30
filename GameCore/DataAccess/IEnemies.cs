using GameCore.Entities;

namespace GameCore.DataAccess
{
    public interface IEnemies
    {
        public Task<IEnumerable<Enemy>> AllEnemiesAsync();
        public Task<Enemy?> GetEnemyAsync(int enemyId);
        public Task<Enemy> GetRandomEnemyAsync(int zoneId);
    }
}
