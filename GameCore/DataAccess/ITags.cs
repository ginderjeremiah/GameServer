using GameCore.Entities.Tags;

namespace GameCore.DataAccess
{
    public interface ITags
    {
        public List<Tag> AllTags();
        public List<Tag> TagsForItem(int itemId);
        public List<Tag> TagsForItemMod(int itemModId);
        public void SetItemTags(int itemId, IEnumerable<int> tagIds);
        public void SetItemModTags(int itemModId, IEnumerable<int> tagIds);
        public void AddTag(string tagName, int tagCategoryId);
        public void UpdateTag(int tagId, string tagName, int tagCategoryId);
        public void DeleteTag(int tagId);
    }
}
