using DataAccess.Entities.ItemCategories;
using GameCore.Database.Interfaces;

namespace DataAccess.Repositories
{
    internal class ItemCategories : BaseRepository, IItemCategories
    {
        public ItemCategories(IDataProvider database) : base(database) { }

        public List<ItemCategory> GetItemCategories()
        {
            var commandText = @"
                SELECT
                    ItemCategoryId,
                    CategoryName
                FROM ItemCategories";

            return Database.QueryToList<ItemCategory>(commandText);
        }
    }

    public interface IItemCategories
    {
        public List<ItemCategory> GetItemCategories();
    }
}
