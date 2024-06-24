using GameCore.DataAccess;
using GameCore.Entities;

namespace GameTests.Mocks.DataAccess.Repositories
{
    internal class MockSlotTypes : ISlotTypes
    {
        public List<SlotType> SlotTypes { get; set; } = new();
        public List<SlotType> AllSlotTypes()
        {
            return SlotTypes;
        }
    }
}
