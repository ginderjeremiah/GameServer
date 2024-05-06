using GameCore.DataAccess;
using GameCore.Entities.ItemCategories;

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
