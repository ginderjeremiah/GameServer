using Game.Infrastructure.Database;
using Game.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Game.DataAccess.Repositories.Caching
{
    /// <summary>Singleton snapshot holder for the cached item entity list.</summary>
    internal sealed class ItemsCacheHolder(IServiceScopeFactory scopeFactory)
        : ReferenceCacheHolder<IReadOnlyList<Item>>(scopeFactory)
    {
        protected override async Task<IReadOnlyList<Item>> BuildSnapshotAsync(GameContext context, CancellationToken cancellationToken)
        {
            return await context.Items
                .AsNoTracking()
                .Include(i => i.ItemModSlots)
                .Include(i => i.ItemAttributes)
                .Include(i => i.Tags)
                .AsSplitQuery()
                .OrderBy(i => i.Id)
                .ToListAsync(cancellationToken);
        }
    }
}
