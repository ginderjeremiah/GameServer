using GameCore.DataAccess;
using GameCore.Entities;
using GameInfrastructure.Database;

namespace DataAccess.Repositories
{
    internal class TagCategories : BaseRepository, ITagCategories
    {
        public TagCategories(GameContext database) : base(database) { }

        public IQueryable<TagCategory> AllTagCategories()
        {
            return Database.TagCategories;
        }
    }
}
