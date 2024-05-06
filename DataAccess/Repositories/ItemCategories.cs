using GameCore.DataAccess;
using GameCore.Entities.ItemCategories;
using GameCore.Infrastructure;

namespace DataAccess.Repositories
{
    internal class ItemCategories : BaseRepository, IItemCategories
    {
        public ItemCategories(IDatabaseService database) : base(database) { }

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
}
