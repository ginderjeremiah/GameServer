using Contracts = Game.Abstractions.Contracts;

namespace Game.Abstractions.DataAccess
{
    public interface IItemCategories
    {
        public IAsyncEnumerable<Contracts.ItemCategory> All();
    }
}
