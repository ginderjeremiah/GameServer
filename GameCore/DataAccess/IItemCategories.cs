using GameCore.Entities.ItemCategories;

namespace GameCore.DataAccess
{
    public interface IItemCategories
    {
        public List<ItemCategory> GetItemCategories();
    }
}
