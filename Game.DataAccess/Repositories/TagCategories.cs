using Game.Abstractions.DataAccess;
using Game.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Contracts = Game.Abstractions.Contracts;


namespace Game.DataAccess.Repositories
{
    internal class TagCategories : ITagCategories
    {
        private readonly GameContext _context;

        public TagCategories(GameContext context)
        {
            _context = context;
        }

        public IAsyncEnumerable<Contracts.TagCategory> All()
        {
            return _context.TagCategories
                .Select(tc => new Contracts.TagCategory { Id = tc.Id, Name = tc.Name })
                .AsAsyncEnumerable();
        }
    }
}
