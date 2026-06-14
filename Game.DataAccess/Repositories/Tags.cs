using System.Linq.Expressions;
using Game.Abstractions.DataAccess;
using Game.Infrastructure.Entities;
using Game.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Contracts = Game.Abstractions.Contracts;

namespace Game.DataAccess.Repositories
{
    internal class Tags : ITags, ITagAssignmentQueries
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

        public IAsyncEnumerable<int> GetExistingTagIds(IEnumerable<int> tagIds)
        {
            return _context.Tags.Where(t => tagIds.Contains(t.Id)).Select(t => t.Id).AsAsyncEnumerable();
        }

        public IAsyncEnumerable<int> GetTagIdsForItem(int itemId)
        {
            return _context.ItemTags.Where(it => it.ItemId == itemId).Select(it => it.TagId).AsAsyncEnumerable();
        }

        public IAsyncEnumerable<int> GetTagIdsForItemMod(int itemModId)
        {
            return _context.ItemModTags.Where(imt => imt.ItemModId == itemModId).Select(imt => imt.TagId).AsAsyncEnumerable();
        }
    }
}
