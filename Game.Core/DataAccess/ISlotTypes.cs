using Game.Core.Entities;

namespace Game.Core.DataAccess
{
    public interface IItemModSlotTypes
    {
        public IAsyncEnumerable<ItemModSlotType> All();
    }
}
