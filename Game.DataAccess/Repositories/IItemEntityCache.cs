using ItemEntity = Game.Infrastructure.Entities.Item;

namespace Game.DataAccess.Repositories
{
    /// <summary>
    /// Internal access to the cached item <em>entities</em> for the Content Authoring admin persistence
    /// (<see cref="Repositories.Admin.AdminItems"/>), which needs the EF entity for existence/diff lookups.
    /// Kept out of the public <see cref="Abstractions.DataAccess.IItems"/> read contract — the entity is an
    /// implementation detail of this layer.
    /// </summary>
    internal interface IItemEntityCache
    {
        /// <summary>The cached item entity at <paramref name="itemId"/> (its zero-based index), or null if out of range.</summary>
        ItemEntity? LookupItem(int itemId);
    }
}
