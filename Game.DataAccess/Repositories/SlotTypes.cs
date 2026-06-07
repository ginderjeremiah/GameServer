using Game.Abstractions.DataAccess;
using Game.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Contracts = Game.Abstractions.Contracts;


namespace Game.DataAccess.Repositories
{
    internal class ItemModTypes : IItemModTypes
    {
        private readonly GameContext _context;

        public ItemModTypes(GameContext context)
        {
            _context = context;
        }

        public IAsyncEnumerable<Contracts.ItemModType> All()
        {
            return _context.ItemModTypes
                .Select(t => new Contracts.ItemModType { Id = t.Id, Name = t.Name })
                .AsAsyncEnumerable();
        }
    }
}
