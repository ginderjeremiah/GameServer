using Game.DataAccess.Mapping;
using Game.Infrastructure.Database;
using Game.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using CoreZone = Game.Core.Zones.Zone;

namespace Game.DataAccess.Repositories.Caching
{
    /// <summary>
    /// An immutable snapshot of the zone reference set: the ordered zone entity list and the lean
    /// gameplay <see cref="CoreZone"/>s the per-battle setup reads hand back (aligned by zero-based id with
    /// the entity list). Both are built and published together so a reader can never observe a new entity
    /// list against stale (or null) domain models.
    /// </summary>
    internal sealed record ZoneSnapshot(
        IReadOnlyList<Zone> Entities,
        IReadOnlyList<CoreZone> CoreZones);

    /// <summary>Singleton snapshot holder for the cached zone list and its pre-materialized domain models.</summary>
    internal sealed class ZonesCacheHolder(IServiceScopeFactory scopeFactory)
        : ReferenceCacheHolder<ZoneSnapshot>(scopeFactory)
    {
        protected override async Task<ZoneSnapshot> BuildSnapshotAsync(GameContext context, CancellationToken cancellationToken)
        {
            var zones = await context.Zones
                .AsNoTracking()
                .Include(z => z.ZoneEnemies)
                .OrderBy(z => z.Id)
                .ToListAsync(cancellationToken);

            zones.AssertZeroBasedContiguity("Zones");

            // Pre-map the lean domain models once here (aligned by id with the entity list) so the gameplay
            // reads hand back a shared instance rather than re-mapping a fresh CoreZone per battle setup.
            var coreZones = zones.Select(ZoneMapper.ToCore).ToList();

            return new ZoneSnapshot(zones, coreZones);
        }
    }
}
