using Game.Core.DataAccess;
using Game.Core.Entities;
using Game.Infrastructure.Database;

namespace Game.DataAccess.Repositories
{
    internal class ItemCategories : IItemCategories
    {
        private readonly GameContext _context;

        public ItemCategories(GameContext context)
        {
            _context = context;
        }

        public IAsyncEnumerable<ItemCategory> All()
        {
            return _context.ItemCategories.AsAsyncEnumerable();
        }
    }
}
