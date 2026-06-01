using Game.Abstractions.DataAccess;
using Game.Abstractions.Entities;
using Game.Infrastructure.Database;

using Microsoft.EntityFrameworkCore;

namespace Game.DataAccess.Repositories
{
    internal class Tags : ITags
    {
        private readonly GameContext _context;

        public Tags(GameContext context)
        {
            _context = context;
        }

        public IAsyncEnumerable<Tag> All()
        {
            return _context.Tags.AsAsyncEnumerable();
        }

        public IAsyncEnumerable<Tag> GetTags(IEnumerable<int> tagIds)
        {
            return _context.Tags.Where(t => tagIds.Contains(t.Id)).AsAsyncEnumerable();
        }

        public IAsyncEnumerable<Tag> GetTagsForItem(int itemId)
        {
            return _context.Tags
                .Include(t => t.Items)
                .Where(t => t.Items.Any(im => im.Id == itemId))
                .AsAsyncEnumerable();
        }

        public IAsyncEnumerable<Tag> GetTagsForItemMod(int itemModId)
        {
            return _context.Tags
                .Include(t => t.ItemMods)
                .Where(t => t.ItemMods.Any(im => im.Id == itemModId))
                .AsAsyncEnumerable();
        }
    }
}
