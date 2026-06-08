using TagEntity = Game.Infrastructure.Entities.Tag;

namespace Game.DataAccess.Repositories
{
    /// <summary>
    /// Internal access to tag <em>entities</em> (with their item / item-mod join navigations) for the
    /// Content Authoring admin persistence, which mutates those navigations directly. Distinct from the
    /// public <see cref="Abstractions.DataAccess.ITags"/> read contracts — the entity is an implementation
    /// detail of this layer. Unlike the cached reference repos these query the database directly.
    /// </summary>
    internal interface ITagEntityQueries
    {
        IAsyncEnumerable<TagEntity> GetTags(IEnumerable<int> tagIds);
        IAsyncEnumerable<TagEntity> GetTagEntitiesForItem(int itemId);
        IAsyncEnumerable<TagEntity> GetTagEntitiesForItemMod(int itemModId);
    }
}
