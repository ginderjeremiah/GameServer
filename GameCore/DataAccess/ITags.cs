using GameCore.Entities;

namespace GameCore.DataAccess
{
    public interface ITags
    {
        public IQueryable<Tag> AllTags();
        public IQueryable<Tag> TagsForItem(int itemId);
        public IQueryable<Tag> TagsForItemMod(int itemModId);
    }
}
