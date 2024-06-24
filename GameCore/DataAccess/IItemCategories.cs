using GameCore.Entities;

namespace GameCore.DataAccess
{
    public interface IItemCategories
    {
        public IQueryable<ItemCategory> AllItemCategories();
    }
}
