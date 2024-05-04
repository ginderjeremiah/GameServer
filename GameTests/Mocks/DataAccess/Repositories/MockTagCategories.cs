using DataAccess.Entities.TagCategories;
using DataAccess.Repositories;

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
