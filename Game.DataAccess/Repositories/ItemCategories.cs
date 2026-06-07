using Game.Abstractions.DataAccess;
using Game.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Contracts = Game.Abstractions.Contracts;

namespace Game.DataAccess.Repositories
{
    internal class ItemCategories : IItemCategories
    {
        private readonly GameContext _context;

        public ItemCategories(GameContext context)
        {
            _context = context;
        }

        public IAsyncEnumerable<Contracts.ItemCategory> All()
        {
            return _context.ItemCategories
                .Select(c => new Contracts.ItemCategory { Id = c.Id, Name = c.Name })
                .AsAsyncEnumerable();
        }
    }
}
