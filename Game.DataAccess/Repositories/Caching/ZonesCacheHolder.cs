using Game.Infrastructure.Database;
using Game.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Game.DataAccess.Repositories.Caching
{
    /// <summary>Singleton snapshot holder for the cached zone entity list.</summary>
    internal sealed class ZonesCacheHolder(IServiceScopeFactory scopeFactory)
        : ReferenceCacheHolder<IReadOnlyList<Zone>>(scopeFactory)
    {
        protected override async Task<IReadOnlyList<Zone>> BuildSnapshotAsync(GameContext context, CancellationToken cancellationToken)
        {
            return await context.Zones
                .AsNoTracking()
                .Include(z => z.ZoneEnemies)
                .OrderBy(z => z.Id)
                .ToListAsync(cancellationToken);
        }
    }
}
