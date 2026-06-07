using Contracts = Game.Abstractions.Contracts;

namespace Game.Abstractions.DataAccess
{
    public interface IItemModTypes
    {
        public IAsyncEnumerable<Contracts.ItemModType> All();
    }
}
