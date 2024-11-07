using Game.Core.Entities;

namespace Game.Core.DataAccess
{
    public interface IItemCategories
    {
        public IAsyncEnumerable<ItemCategory> All();
    }
}
