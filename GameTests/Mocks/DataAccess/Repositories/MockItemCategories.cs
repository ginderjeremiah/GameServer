using DataAccess.Entities.ItemCategories;
using DataAccess.Repositories;

namespace GameTests.Mocks.DataAccess.Repositories
{
    internal class MockItemCategories : IItemCategories
    {
        public List<ItemCategory> GetItemCategories()
        {
            throw new NotImplementedException();
        }
    }
}
