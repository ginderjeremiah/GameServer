using Game.Core.DataAccess;
using Game.Core.Entities;
using Game.Infrastructure.Database;

namespace Game.DataAccess.Repositories
{
    internal class TagCategories : ITagCategories
    {
        private readonly GameContext _context;

        public TagCategories(GameContext context)
        {
            _context = context;
        }

        public IAsyncEnumerable<TagCategory> All()
        {
            return _context.TagCategories.AsAsyncEnumerable();
        }
    }
}
