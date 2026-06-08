using ItemModEntity = Game.Infrastructure.Entities.ItemMod;

namespace Game.DataAccess.Repositories
{
    /// <summary>
    /// Internal access to the cached item-mod <em>entities</em> for the Content Authoring admin persistence
    /// (<see cref="Repositories.Admin.AdminItemMods"/>), which needs the EF entity for existence/diff lookups.
    /// Kept out of the public <see cref="Abstractions.DataAccess.IItemMods"/> read contract — the entity is an
    /// implementation detail of this layer.
    /// </summary>
    internal interface IItemModEntityCache
    {
        /// <summary>The cached item-mod entity at <paramref name="itemModId"/> (its zero-based index), or null if out of range.</summary>
        ItemModEntity? LookupItemMod(int itemModId);
    }
}
