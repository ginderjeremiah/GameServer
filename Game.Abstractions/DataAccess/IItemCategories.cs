using Game.Abstractions.Entities;

namespace Game.Abstractions.DataAccess
{
    public interface IItemCategories
    {
        public IAsyncEnumerable<ItemCategory> All();
    }
}
