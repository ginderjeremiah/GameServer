using DataAccess.Entities.ItemCategories;

namespace DataAccess.Repositories
{
    internal class ItemCategories : BaseRepository, IItemCategories
    {
        public ItemCategories(string connectionString) : base(connectionString) { }

        public List<ItemCategory> GetItemCategories()
        {
            var commandText = @"
                SELECT
                    ItemCategoryId,
                    CategoryName
                FROM ItemCategories";

            return QueryToList<ItemCategory>(commandText);
        }
    }

    public interface IItemCategories
    {
        public List<ItemCategory> GetItemCategories();
    }
}
