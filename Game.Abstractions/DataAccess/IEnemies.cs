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

        /// <summary>Whether the zone has a non-empty random spawn table — i.e. an idle encounter can be
        /// rolled there. False for an unknown zone id or a zone every spawn enemy of which is retired.</summary>
        public bool HasSpawnableEnemies(int zoneId);

        /// <inheritdoc cref="IItems.VersionKey"/>
        public object VersionKey { get; }
    }
}
