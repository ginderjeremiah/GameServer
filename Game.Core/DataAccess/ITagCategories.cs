using Game.Core.Entities;

namespace Game.Core.DataAccess
{
    public interface ITagCategories
    {
        public IAsyncEnumerable<TagCategory> All();
    }
}
