using DataAccess.Models.TagCategories;
namespace DataAccess.Repositories
{
    internal class TagCategories : BaseRepository, ITagCategories
    {
        public TagCategories(string connectionString) : base(connectionString) { }

        public List<TagCategory> GetTagCategories()
        {
            var commandText = @"
                SELECT
                    TagCategoryId,
                    TagCategoryName
                FROM TagCategories";

            return QueryToList<TagCategory>(commandText);
        }
    }

    public interface ITagCategories
    {
        public List<TagCategory> GetTagCategories();
    }
}
