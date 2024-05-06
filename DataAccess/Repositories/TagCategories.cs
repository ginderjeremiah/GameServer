using GameCore.DataAccess;
using GameCore.Entities.TagCategories;
using GameCore.Infrastructure;

namespace DataAccess.Repositories
{
    internal class TagCategories : BaseRepository, ITagCategories
    {
        public TagCategories(IDatabaseService database) : base(database) { }

        public List<TagCategory> GetTagCategories()
        {
            var commandText = @"
                SELECT
                    TagCategoryId,
                    TagCategoryName
                FROM TagCategories";

            return Database.QueryToList<TagCategory>(commandText);
        }
    }
}
