using GameCore.DataAccess;
using GameCore.Entities.ItemCategories;

namespace GameTests.Mocks.DataAccess.Repositories
{
    internal class MockItemCategories : IItemCategories
    {
        public List<ItemCategory> ItemCategories { get; set; } = new();
        public List<ItemCategory> GetItemCategories()
        {
            return ItemCategories;
        }
    }
}
