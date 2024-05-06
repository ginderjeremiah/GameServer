using GameCore.DataAccess;
using GameCore.Entities.TagCategories;

namespace GameTests.Mocks.DataAccess.Repositories
{
    internal class MockTagCategories : ITagCategories
    {
        public List<TagCategory> GetTagCategories()
        {
            throw new NotImplementedException();
        }
    }
}
