using System.Linq.Expressions;
using Game.Abstractions.DataAccess;
using Game.Infrastructure.Entities;
using Game.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Contracts = Game.Abstractions.Contracts;

namespace Game.DataAccess.Repositories
{
    internal class Tags : ITags, ITagEntityQueries
    {
        // Single source of truth for the entity -> read-contract projection so EF can translate it in SQL.
        private static readonly Expression<Func<Tag, Contracts.Tag>> ToContract =
            t => new Contracts.Tag { Id = t.Id, Name = t.Name, TagCategoryId = t.TagCategoryId };

        private readonly GameContext _context;

        public Tags(GameContext context)
        {
            _context = context;
        }

        public IAsyncEnumerable<Contracts.Tag> All()
        {
            return _context.Tags.Select(ToContract).AsAsyncEnumerable();
        }

        public IAsyncEnumerable<Contracts.Tag> GetTagsForItem(int itemId)
        {
            return _context.Tags
                .Where(t => t.Items.Any(i => i.Id == itemId))
                .Select(ToContract)
                .AsAsyncEnumerable();
        }

        public IAsyncEnumerable<Contracts.Tag> GetTagsForItemMod(int itemModId)
        {
            return _context.Tags
                .Where(t => t.ItemMods.Any(im => im.Id == itemModId))
                .Select(ToContract)
                .AsAsyncEnumerable();
        }

        public IAsyncEnumerable<Tag> GetTags(IEnumerable<int> tagIds)
        {
            return _context.Tags.Where(t => tagIds.Contains(t.Id)).AsAsyncEnumerable();
        }

        public IAsyncEnumerable<Tag> GetTagEntitiesForItem(int itemId)
        {
            return _context.Tags
                .Include(t => t.Items)
                .Where(t => t.Items.Any(im => im.Id == itemId))
                .AsAsyncEnumerable();
        }

        public IAsyncEnumerable<Tag> GetTagEntitiesForItemMod(int itemModId)
        {
            return _context.Tags
                .Include(t => t.ItemMods)
                .Where(t => t.ItemMods.Any(im => im.Id == itemModId))
                .AsAsyncEnumerable();
        }
    }
}
