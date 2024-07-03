using GameCore.DataAccess;
using GameCore.Entities;
using GameInfrastructure.Database;

namespace DataAccess.Repositories
{
    internal class ItemCategories : BaseRepository, IItemCategories
    {
        public ItemCategories(GameContext database) : base(database) { }

        public IQueryable<ItemCategory> AllItemCategories()
        {
            return Database.ItemCategories;
        }
    }
}
