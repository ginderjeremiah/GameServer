using GameCore.DataAccess;
using GameCore.Entities;
using GameCore.Infrastructure;

namespace DataAccess.Repositories
{
    internal class ItemCategories : BaseRepository, IItemCategories
    {
        public ItemCategories(IDatabaseService database) : base(database) { }

        public IQueryable<ItemCategory> AllItemCategories()
        {
            return Database.ItemCategories;
        }
    }
}
