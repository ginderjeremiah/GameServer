using Game.DataAccess.Mapping;
using Game.Infrastructure.Database;
using Game.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using CoreProficiency = Game.Core.Proficiencies.Proficiency;
using Path = Game.Infrastructure.Entities.Path;
using SkillContribution = Game.Core.Proficiencies.SkillContribution;

namespace Game.DataAccess.Repositories.Caching
{
    /// <summary>
    /// An immutable snapshot of the proficiency reference set: the ordered proficiency and path entity lists
    /// (contract projection and admin entity lookups), the pre-materialized lean <see cref="CoreProficiency"/>
    /// domain models, and the derived skill → contributions reverse index. All are built and published
    /// together so a reader can never observe a new entity list against a stale index.
    /// <para>
    /// Skill contributions now target a <see cref="Path"/> at a home tier. The reverse index resolves each
    /// contribution to the proficiency at that tier so the merged XP accrual (#1116) keeps its
    /// proficiency-keyed shape; the home-tier-falloff routing that consumes the path/tier directly lands in
    /// #1161.
    /// </para>
    /// </summary>
    internal sealed record ProficiencySnapshot(
        IReadOnlyList<Proficiency> Entities,
        IReadOnlyList<Path> Paths,
        IReadOnlyList<CoreProficiency> CoreProficiencies,
        IReadOnlyDictionary<int, IReadOnlyList<SkillContribution>> ContributionsBySkill);

    /// <summary>Singleton snapshot holder for the cached proficiency/path entity lists and their derived structures.</summary>
    internal sealed class ProficienciesCacheHolder(IServiceScopeFactory scopeFactory)
        : ReferenceCacheHolder<ProficiencySnapshot>(scopeFactory)
    {
        protected override async Task<ProficiencySnapshot> BuildSnapshotAsync(GameContext context, CancellationToken cancellationToken)
        {
            var entities = await context.Proficiencies
                .AsNoTracking()
                .Include(p => p.LevelModifiers)
                .Include(p => p.LevelRewards)
                .Include(p => p.Prerequisites)
                .AsSplitQuery()
                .OrderBy(p => p.Id)
                .ToListAsync(cancellationToken);

            entities.AssertZeroBasedContiguity("Proficiencies");

            var paths = await context.Paths
                .AsNoTracking()
                .Include(p => p.SkillContributions)
                .OrderBy(p => p.Id)
                .ToListAsync(cancellationToken);

            paths.AssertZeroBasedContiguity("Paths");

            // The proficiency at each (path, tier). The (PathId, PathOrdinal) unique index keeps this
            // unambiguous, and the admin contributions save validates a contribution's home tier resolves
            // here, so the lookup below never misses on authored content.
            var proficiencyIdByTier = entities.ToDictionary(p => (p.PathId, p.PathOrdinal), p => p.Id);

            // Shim (removed by #1161): map each path-targeted contribution to the proficiency at its home
            // tier so the accrual's existing skill → {proficiency, weight} reverse index is preserved.
            var contributionsBySkill = paths
                .SelectMany(path => path.SkillContributions.Select(c => (c.SkillId, Contribution: new SkillContribution
                {
                    ProficiencyId = proficiencyIdByTier[(path.Id, c.HomeTier)],
                    Weight = (double)c.Weight,
                })))
                .GroupBy(x => x.SkillId)
                .ToDictionary(
                    g => g.Key,
                    g => (IReadOnlyList<SkillContribution>)g.Select(x => x.Contribution).ToList());

            return new ProficiencySnapshot(
                entities,
                paths,
                entities.Select(ProficiencyMapper.ToCore).ToList(),
                contributionsBySkill);
        }
    }
}
