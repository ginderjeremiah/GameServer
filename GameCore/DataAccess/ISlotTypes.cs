using GameCore.Entities;

namespace GameCore.DataAccess
{
    public interface ISlotTypes
    {
        public IQueryable<ItemModSlotType> AllItemModSlotTypes();
    }
}
