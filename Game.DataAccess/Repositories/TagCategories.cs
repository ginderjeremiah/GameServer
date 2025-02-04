using Game.Abstractions.DataAccess;
using Game.Abstractions.Entities;
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
