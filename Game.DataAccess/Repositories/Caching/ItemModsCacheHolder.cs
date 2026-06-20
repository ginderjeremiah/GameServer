using Game.DataAccess.Mapping;
using Game.Infrastructure.Database;
using Game.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using CoreItemMod = Game.Core.Items.ItemMod;

namespace Game.DataAccess.Repositories.Caching
{
    /// <summary>
    /// An immutable snapshot of the item-mod reference set: the ordered entity list (used for the contract
    /// <c>All()</c> projection and the admin entity lookups) plus the pre-materialized lean
    /// <see cref="CoreItemMod"/> domain models, built and published together so a reader can never observe a
    /// new entity list against stale core models. The shared <see cref="CoreItemMod"/> instances are returned
    /// from <c>ItemMods.GetItemMod</c> instead of rebuilding a fresh graph per call (see <see cref="ItemSnapshot"/>).
    /// </summary>
    internal sealed record ItemModSnapshot(
        IReadOnlyList<ItemMod> Entities,
        IReadOnlyList<CoreItemMod> CoreItemMods);

    /// <summary>Singleton snapshot holder for the cached item-mod entity list and its derived core models.</summary>
    internal sealed class ItemModsCacheHolder(IServiceScopeFactory scopeFactory)
        : ReferenceCacheHolder<ItemModSnapshot>(scopeFactory)
    {
        protected override async Task<ItemModSnapshot> BuildSnapshotAsync(GameContext context, CancellationToken cancellationToken)
        {
            var entities = await context.ItemMods
                .AsNoTracking()
                .Include(im => im.ItemModAttributes)
                .Include(im => im.Tags)
                .AsSplitQuery()
                .OrderBy(im => im.Id)
                .ToListAsync(cancellationToken);

            entities.AssertZeroBasedContiguity("ItemMods");

            return new ItemModSnapshot(entities, entities.Select(ItemMapper.ModToCore).ToList());
        }
    }
}
