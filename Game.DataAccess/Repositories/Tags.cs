using Game.Core.DataAccess;
using Game.Core.Entities;
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
            return _context.Items
                .Include(i => i.Tags)
                .Where(i => i.Id == itemId)
                .SelectMany(i => i.Tags)
                .AsAsyncEnumerable();
        }

        public IAsyncEnumerable<Tag> GetTagsForItemMod(int itemModId)
        {
            return _context.ItemMods
                .Include(im => im.Tags)
                .Where(im => im.Id == itemModId)
                .SelectMany(i => i.Tags)
                .AsAsyncEnumerable();
        }
    }
}
