using Game.Core.Probability;
using Game.DataAccess.Mapping;
using Game.Infrastructure.Database;
using Game.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using EnemyTemplate = Game.Core.Enemies.EnemyTemplate;

namespace Game.DataAccess.Repositories.Caching
{
    /// <summary>
    /// An immutable snapshot of the enemy reference set: the ordered enemy entity list, the level-independent
    /// pre-mapped <see cref="EnemyTemplate"/>s the gameplay reads clone from (aligned by zero-based id with the
    /// entity list), and the per-zone spawn tables derived from the list. All are built and published together
    /// so a reader can never observe a new enemy list against stale (or null) templates or spawn tables.
    /// </summary>
    internal sealed record EnemySnapshot(
        IReadOnlyList<Enemy> Enemies,
        IReadOnlyList<EnemyTemplate> Templates,
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
                .AsSplitQuery()
                .OrderBy(e => e.Id)
                .ToListAsync(cancellationToken);

            // Map each enemy's available-skill loadout once here so the gameplay reads clone a pre-built
            // template rather than re-mapping the skill graph per encounter (#584). Skills are queried directly
            // from this snapshot's own context rather than read from the skill cache so the build stays
            // self-contained and order-independent: holders reload concurrently and independently (no holder
            // depends on another's reload order), so reading the shared skill cache mid-sweep could observe a
            // stale — or not-yet-loaded — skill snapshot.
            var skills = await context.Skills
                .AsNoTracking()
                .Include(s => s.SkillDamageMultipliers)
                .Include(s => s.SkillEffects)
                .AsSplitQuery()
                .OrderBy(s => s.Id)
                .ToListAsync(cancellationToken);

            var templates = enemies.Select(enemy => EnemyMapper.ToTemplate(enemy, skills)).ToList();

            return new EnemySnapshot(enemies, templates, BuildZoneEnemyTables(enemies));
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
