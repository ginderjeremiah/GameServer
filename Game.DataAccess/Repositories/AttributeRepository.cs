using Game.Core.DataAccess;
using Game.Infrastructure.Database;
using Attribute = Game.Core.Entities.Attribute;

namespace Game.DataAccess.Repositories
{
    internal class Attributes : IAttributes
    {
        private readonly GameContext _context;
        public Attributes(GameContext context)
        {
            _context = context;
        }

        public IAsyncEnumerable<Attribute> All()
        {
            return _context.Attributes.AsAsyncEnumerable();
        }
    }
}
