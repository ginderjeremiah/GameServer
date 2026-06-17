using Contracts = Game.Abstractions.Contracts;
using CoreEnemy = Game.Core.Enemies.Enemy;

namespace Game.Abstractions.DataAccess
{
    public interface IEnemies
    {
        public List<Contracts.Enemy> All();

        /// <summary>Maps the entity enemy with <paramref name="level"/> to a domain
        /// <see cref="CoreEnemy"/>, resolving skill and item references from the catalog.</summary>
        public CoreEnemy? GetDomainEnemy(int enemyId, int level);

        /// <inheritdoc cref="GetDomainEnemy"/>
        public CoreEnemy GetRandomDomainEnemy(int zoneId, int level);

        /// <inheritdoc cref="IItems.VersionKey"/>
        public object VersionKey { get; }
    }
}
