using Contracts = Game.Abstractions.Contracts;
using TagEntity = Game.Abstractions.Entities.Tag;

namespace Game.Abstractions.DataAccess
{
    public interface ITags
    {
        // Read contracts (the published reference-data read language).
        public IAsyncEnumerable<Contracts.Tag> All();
        public IAsyncEnumerable<Contracts.Tag> GetTagsForItem(int itemId);
        public IAsyncEnumerable<Contracts.Tag> GetTagsForItemMod(int itemModId);

        // EF entity access for the Content Authoring admin persistence (Game.DataAccess), which mutates the
        // tag <-> item/item-mod join navigations directly. Kept distinct from the read contracts above.
        public IAsyncEnumerable<TagEntity> GetTags(IEnumerable<int> tagIds);
        public IAsyncEnumerable<TagEntity> GetTagEntitiesForItem(int itemId);
        public IAsyncEnumerable<TagEntity> GetTagEntitiesForItemMod(int itemModId);
    }
}
