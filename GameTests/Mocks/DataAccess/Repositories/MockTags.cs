using DataAccess.Entities.Tags;
using DataAccess.Repositories;

namespace GameTests.Mocks.DataAccess.Repositories
{
    internal class MockTags : ITags
    {
        public void AddTag(string tagName, int tagCategoryId)
        {
            throw new NotImplementedException();
        }

        public List<Tag> AllTags()
        {
            throw new NotImplementedException();
        }

        public void DeleteTag(int tagId)
        {
            throw new NotImplementedException();
        }

        public void SetItemModTags(int itemModId, IEnumerable<int> tagIds)
        {
            throw new NotImplementedException();
        }

        public void SetItemTags(int itemId, IEnumerable<int> tagIds)
        {
            throw new NotImplementedException();
        }

        public List<Tag> TagsForItem(int itemId)
        {
            throw new NotImplementedException();
        }

        public List<Tag> TagsForItemMod(int itemModId)
        {
            throw new NotImplementedException();
        }

        public void UpdateTag(int tagId, string tagName, int tagCategoryId)
        {
            throw new NotImplementedException();
        }
    }
}
