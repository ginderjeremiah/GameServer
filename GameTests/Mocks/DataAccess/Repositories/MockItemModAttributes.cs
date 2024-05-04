using DataAccess.Repositories;

namespace GameTests.Mocks.DataAccess.Repositories
{
    internal class MockItemModAttributes : IItemModAttributes
    {
        public void AddItemModAttribute(int itemModId, int attributeId, decimal amount)
        {
            throw new NotImplementedException();
        }

        public void DeleteItemModAttribute(int itemModId, int attributeId)
        {
            throw new NotImplementedException();
        }

        public void UpdateItemModAttribute(int itemModId, int attributeId, decimal amount)
        {
            throw new NotImplementedException();
        }
    }
}
