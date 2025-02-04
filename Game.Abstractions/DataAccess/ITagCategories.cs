using Game.Abstractions.Entities;

namespace Game.Abstractions.DataAccess
{
    public interface ITagCategories
    {
        public IAsyncEnumerable<TagCategory> All();
    }
}
