using DataAccess.Entities.TagCategories;
using GameLibrary.Database.Interfaces;

namespace DataAccess.Repositories
{
    internal class TagCategories : BaseRepository, ITagCategories
    {
        public TagCategories(IDataProvider database) : base(database) { }

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

    public interface ITagCategories
    {
        public List<TagCategory> GetTagCategories();
    }
}
