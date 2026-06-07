using Contracts = Game.Abstractions.Contracts;
using EnemyEntity = Game.Abstractions.Entities.Enemy;
using CoreEnemy = Game.Core.Enemies.Enemy;

namespace Game.Abstractions.DataAccess
{
    public interface IEnemies
    {
        public void InvalidateCache();
        public List<Contracts.Enemy> All(bool refreshCache = false);
        // Returns the EF entity for the admin Content Authoring write path (#135); the read path uses the contracts above.
        public EnemyEntity? GetEnemy(int enemyId);
        public EnemyEntity GetRandomEnemy(int zoneId);

        /// <summary>Maps the entity enemy with <paramref name="level"/> to a domain
        /// <see cref="CoreEnemy"/>, resolving skill and item references from the catalog.</summary>
        public CoreEnemy? GetDomainEnemy(int enemyId, int level);

        /// <inheritdoc cref="GetDomainEnemy"/>
        public CoreEnemy GetRandomDomainEnemy(int zoneId, int level);
    }
}
