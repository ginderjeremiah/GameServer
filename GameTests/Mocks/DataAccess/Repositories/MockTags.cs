using GameCore;
using GameCore.DataAccess;
using GameCore.Entities.Tags;

namespace GameTests.Mocks.DataAccess.Repositories
{
    internal class MockTags : ITags
    {
        public List<Tag> Tags { get; set; } = new();
        public Dictionary<int, List<int>> ItemTagIds = new();
        public Dictionary<int, List<int>> ItemModTagIds = new();
        public void AddTag(string tagName, int tagCategoryId)
        {
            var maxId = Tags.Max(tag => tag.TagId) + 1;
            Tags.Add(new Tag
            {
                TagId = maxId,
                TagName = tagName,
                TagCategoryId = tagCategoryId
            });
        }

        public List<Tag> AllTags()
        {
            return Tags;
        }

        public void DeleteTag(int tagId)
        {
            var tagToRemove = Tags.FirstOrDefault(tag => tag.TagId == tagId);
            if (tagToRemove != null)
            {
                Tags.Remove(tagToRemove);
            }
        }

        public void SetItemModTags(int itemModId, IEnumerable<int> tagIds)
        {
            ItemModTagIds[itemModId] = tagIds.ToList();
        }

        public void SetItemTags(int itemId, IEnumerable<int> tagIds)
        {
            ItemTagIds[itemId] = tagIds.ToList();
        }

        public List<Tag> TagsForItem(int itemId)
        {
            ItemTagIds.TryGetValue(itemId, out var tags);
            return tags?.SelectNotNull(id => Tags.FirstOrDefault(tag => tag.TagId == id)).ToList() ?? new List<Tag>();
        }

        public List<Tag> TagsForItemMod(int itemModId)
        {
            ItemModTagIds.TryGetValue(itemModId, out var tags);
            return tags?.SelectNotNull(id => Tags.FirstOrDefault(tag => tag.TagId == id)).ToList() ?? new List<Tag>();
        }

        public void UpdateTag(int tagId, string tagName, int tagCategoryId)
        {
            var tagToUpdate = Tags.FirstOrDefault(tag => tag.TagId == tagId);
            if (tagToUpdate != null)
            {
                tagToUpdate.TagName = tagName;
                tagToUpdate.TagCategoryId = tagCategoryId;
            }
        }
    }
}
