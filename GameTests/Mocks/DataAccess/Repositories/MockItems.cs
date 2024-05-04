using DataAccess.Entities.Items;
using DataAccess.Repositories;

namespace GameTests.Mocks.DataAccess.Repositories
{
    internal class MockItems : IItems
    {
        public void AddItem(string itemName, string itemDesc, int itemCategoryId, string iconPath)
        {
            throw new NotImplementedException();
        }

        public List<Item> AllItems(bool refreshCache = false)
        {
            throw new NotImplementedException();
        }

        public void DeleteItem(int itemId)
        {
            throw new NotImplementedException();
        }

        public void UpdateItem(int itemId, string itemName, string itemDesc, int itemCategoryId, string iconPath)
        {
            throw new NotImplementedException();
        }
    }
}
