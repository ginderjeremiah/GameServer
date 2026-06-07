using Contracts = Game.Abstractions.Contracts;

namespace Game.Abstractions.DataAccess
{
    public interface ITagCategories
    {
        public IAsyncEnumerable<Contracts.TagCategory> All();
    }
}
