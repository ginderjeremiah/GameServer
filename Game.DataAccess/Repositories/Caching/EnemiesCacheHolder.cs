using Game.Core.Probability;
using Game.Infrastructure.Database;
using Game.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Game.DataAccess.Repositories.Caching
{
    /// <summary>
    /// An immutable snapshot of the enemy reference set: the ordered enemy list plus the per-zone spawn
    /// tables derived from it. Both are built and published together so a reader can never observe a new
    /// enemy list against stale (or null) spawn tables.
    /// </summary>
    internal sealed record EnemySnapshot(
        IReadOnlyList<Enemy> Enemies,
        IReadOnlyDictionary<int, ProbabilityTable<int>> ZoneEnemyTables);

    /// <summary>
    /// Singleton snapshot holder for the cached enemy list and its derived per-zone spawn tables. The
    /// spawn tables are derived from the in-memory enemy list (whose <c>ZoneEnemies</c> weights are
    /// eager-loaded), so they are rebuilt in step with the list inside the snapshot rather than lazily.
    /// </summary>
    internal sealed class EnemiesCacheHolder(IServiceScopeFactory scopeFactory)
        : ReferenceCacheHolder<EnemySnapshot>(scopeFactory)
    {
        protected override async Task<EnemySnapshot> BuildSnapshotAsync(GameContext context, CancellationToken cancellationToken)
        {
            var enemies = await context.Enemies
                .AsNoTracking()
                .Include(e => e.AttributeDistributions)
                .Include(e => e.EnemySkills)
                .Include(e => e.ZoneEnemies)
                .OrderBy(e => e.Id)
                .ToListAsync(cancellationToken);

            return new EnemySnapshot(enemies, BuildZoneEnemyTables(enemies));
        }

        /// <summary>
        /// Builds the per-zone enemy spawn tables from the in-memory enemy list. The ZoneEnemy weights are
        /// already eager-loaded, so no extra query (and no lock) is required, and keying by zone id avoids
        /// unbounded list growth for arbitrary or invalid ids. Retired enemies are excluded so they no
        /// longer roll as random encounters, while still resolving by id for existing references (e.g. an
        /// authored zone boss).
        /// </summary>
        private static Dictionary<int, ProbabilityTable<int>> BuildZoneEnemyTables(IEnumerable<Enemy> enemies)
        {
            return enemies
                .Where(enemy => enemy.RetiredAt is null)
                .SelectMany(enemy => enemy.ZoneEnemies)
                .GroupBy(zoneEnemy => zoneEnemy.ZoneId)
                .ToDictionary(
                    group => group.Key,
                    group => new ProbabilityTable<int>(
                        group.Select(zoneEnemy => new WeightedValue<int>(zoneEnemy.EnemyId, zoneEnemy.Weight))));
        }
    }
}
