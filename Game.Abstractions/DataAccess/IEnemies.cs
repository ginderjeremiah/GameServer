using Game.Abstractions.Entities;
using CoreEnemy = Game.Core.Enemies.Enemy;

namespace Game.Abstractions.DataAccess
{
    public interface IEnemies
    {
        public void InvalidateCache();
        public List<Enemy> All(bool refreshCache = false);
        public Enemy? GetEnemy(int enemyId);
        public Enemy GetRandomEnemy(int zoneId);

        /// <summary>Maps the entity enemy with <paramref name="level"/> to a domain
        /// <see cref="CoreEnemy"/>, resolving skill and item references from the catalog.</summary>
        public CoreEnemy? GetDomainEnemy(int enemyId, int level);

        /// <inheritdoc cref="GetDomainEnemy"/>
        public CoreEnemy GetRandomDomainEnemy(int zoneId, int level);
    }
}
