using EnemyEntity = Game.Infrastructure.Entities.Enemy;

namespace Game.DataAccess.Repositories
{
    /// <summary>
    /// Internal access to the cached enemy <em>entities</em> for the Content Authoring admin persistence
    /// (<see cref="Repositories.Admin.AdminEnemies"/>), which needs the EF entity for existence/diff lookups.
    /// Kept out of the public <see cref="Abstractions.DataAccess.IEnemies"/> read contract — the entity is an
    /// implementation detail of this layer.
    /// </summary>
    internal interface IEnemyEntityCache
    {
        /// <summary>The cached enemy entity at <paramref name="enemyId"/> (its zero-based index), or null if out of range.</summary>
        EnemyEntity? GetEnemy(int enemyId);

        /// <summary>A random enemy entity drawn from the requested zone's spawn table.</summary>
        EnemyEntity GetRandomEnemy(int zoneId);
    }
}
