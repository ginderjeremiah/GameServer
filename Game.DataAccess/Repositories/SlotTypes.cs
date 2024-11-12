using Game.Core.DataAccess;
using Game.Core.Entities;
using Game.Infrastructure.Database;

namespace Game.DataAccess.Repositories
{
    internal class ItemModTypes : IItemModTypes
    {
        private readonly GameContext _context;

        public ItemModTypes(GameContext context)
        {
            _context = context;
        }

        public IAsyncEnumerable<ItemModType> All()
        {
            return _context.ItemModTypes.AsAsyncEnumerable();
        }
    }
}
