using Game.DataAccess.Mapping;
using Game.Infrastructure.Database;
using Game.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using CoreItem = Game.Core.Items.Item;

namespace Game.DataAccess.Repositories.Caching
{
    /// <summary>
    /// An immutable snapshot of the item reference set: the ordered entity list (used for the contract
    /// <c>All()</c> projection and the admin entity lookups) plus the pre-materialized lean
    /// <see cref="CoreItem"/> domain models. Both are built and published together — like
    /// <see cref="EnemySnapshot"/> — so a reader can never observe a new entity list against stale core
    /// models. The shared <see cref="CoreItem"/> instances are returned from <c>Items.GetItem</c> instead
    /// of rebuilding a fresh graph per call, which removes an allocation from the per-battle path.
    /// </summary>
    internal sealed record ItemSnapshot(
        IReadOnlyList<Item> Entities,
        IReadOnlyList<CoreItem> CoreItems);

    /// <summary>Singleton snapshot holder for the cached item entity list and its derived core models.</summary>
    internal sealed class ItemsCacheHolder(IServiceScopeFactory scopeFactory)
        : ReferenceCacheHolder<ItemSnapshot>(scopeFactory)
    {
        protected override async Task<ItemSnapshot> BuildSnapshotAsync(GameContext context, CancellationToken cancellationToken)
        {
            var entities = await context.Items
                .AsNoTracking()
                .Include(i => i.ItemModSlots)
                .Include(i => i.ItemAttributes)
                .Include(i => i.Tags)
                .AsSplitQuery()
                .OrderBy(i => i.Id)
                .ToListAsync(cancellationToken);

            entities.AssertZeroBasedContiguity("Items");

            return new ItemSnapshot(entities, entities.Select(ItemMapper.ToCore).ToList());
        }
    }
}
