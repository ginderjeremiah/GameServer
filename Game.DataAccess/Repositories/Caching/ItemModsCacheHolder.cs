using Game.Infrastructure.Database;
using Game.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Game.DataAccess.Repositories.Caching
{
    /// <summary>Singleton snapshot holder for the cached item-mod entity list.</summary>
    internal sealed class ItemModsCacheHolder(IServiceScopeFactory scopeFactory)
        : ReferenceCacheHolder<IReadOnlyList<ItemMod>>(scopeFactory)
    {
        protected override async Task<IReadOnlyList<ItemMod>> BuildSnapshotAsync(GameContext context, CancellationToken cancellationToken)
        {
            return await context.ItemMods
                .AsNoTracking()
                .Include(im => im.ItemModAttributes)
                .Include(im => im.Tags)
                .OrderBy(im => im.Id)
                .ToListAsync(cancellationToken);
        }
    }
}
