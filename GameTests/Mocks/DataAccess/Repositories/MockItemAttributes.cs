using DataAccess.Repositories;

namespace GameTests.Mocks.DataAccess.Repositories
{
    internal class MockItemAttributes : IItemAttributes
    {
        public void AddItemAttribute(int itemId, int attributeId, decimal amount)
        {
            throw new NotImplementedException();
        }

        public void DeleteItemAttribute(int itemId, int attributeId)
        {
            throw new NotImplementedException();
        }

        public void UpdateItemAttribute(int itemId, int attributeId, decimal amount)
        {
            throw new NotImplementedException();
        }
    }
}
