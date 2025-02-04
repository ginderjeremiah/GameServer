using Game.Abstractions.Entities;

namespace Game.Abstractions.DataAccess
{
    public interface IItemModTypes
    {
        public IAsyncEnumerable<ItemModType> All();
    }
}
