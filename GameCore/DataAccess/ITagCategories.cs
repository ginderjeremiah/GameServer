using GameCore.Entities;

namespace GameCore.DataAccess
{
    public interface ITagCategories
    {
        public IQueryable<TagCategory> AllTagCategories();
    }
}
