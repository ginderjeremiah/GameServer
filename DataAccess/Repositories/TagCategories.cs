using GameCore.DataAccess;
using GameCore.Entities;
using GameCore.Infrastructure;

namespace DataAccess.Repositories
{
    internal class TagCategories : BaseRepository, ITagCategories
    {
        public TagCategories(IDatabaseService database) : base(database) { }

        public IQueryable<TagCategory> AllTagCategories()
        {
            return Database.TagCategories;
        }
    }
}
