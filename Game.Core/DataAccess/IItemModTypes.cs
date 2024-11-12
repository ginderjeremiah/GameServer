using Game.Core.Entities;

namespace Game.Core.DataAccess
{
    public interface IItemModTypes
    {
        public IAsyncEnumerable<ItemModType> All();
    }
}
