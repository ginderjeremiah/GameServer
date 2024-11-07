using Game.Core.DataAccess;
using Game.Core.Entities;
using Game.Infrastructure.Database;

namespace Game.DataAccess.Repositories
{
    internal class ItemModSlotTypes : IItemModSlotTypes
    {
        private readonly GameContext _context;

        public ItemModSlotTypes(GameContext context)
        {
            _context = context;
        }

        public IAsyncEnumerable<ItemModSlotType> All()
        {
            return _context.ItemModSlotTypes.AsAsyncEnumerable();
        }
    }
}
