using Game.DataAccess.Mapping;
using Game.Infrastructure.Database;
using Game.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using CorePath = Game.Core.Proficiencies.Path;
using CoreProficiency = Game.Core.Proficiencies.Proficiency;
using Path = Game.Infrastructure.Entities.Path;
using PathTier = Game.Core.Proficiencies.PathTier;
using SkillContribution = Game.Core.Proficiencies.SkillContribution;

namespace Game.DataAccess.Repositories.Caching
{
    /// <summary>
    /// An immutable snapshot of the proficiency reference set: the ordered proficiency and path entity lists
    /// (contract projection and admin entity lookups), the pre-materialized lean <see cref="CoreProficiency"/>
    /// domain models, the <see cref="CorePath"/> routing models, and the derived skill → contributions reverse
    /// index. All are built and published together so a reader can never observe a new entity list against a
    /// stale index.
    /// <para>
    /// A skill contribution targets a <see cref="Path"/> at a home tier. The reverse index exposes the
    /// <c>(PathId, HomeTier, Weight)</c> directly; the battle XP accrual resolves the path's frontier tier and
    /// the home-tier falloff at completion against the <see cref="CorePath"/> models.
    /// </para>
    /// </summary>
    internal sealed record ProficiencySnapshot(
        IReadOnlyList<Proficiency> Entities,
        IReadOnlyList<Path> Paths,
        IReadOnlyList<CoreProficiency> CoreProficiencies,
        IReadOnlyList<CorePath> CorePaths,
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

            var retiredPathIds = paths
                .Where(path => path.RetiredAt is not null)
                .Select(path => path.Id)
                .ToHashSet();

            // The routing models: each path's falloff base plus its tiers ordered by ordinal (the proficiencies
            // carrying its id), so the accrual can resolve a contribution's frontier tier off the player's
            // levels. Built in path-id order so the list is index == id for GetById, matching the entity list.
            var tiersByPath = entities
                .GroupBy(p => p.PathId)
                .ToDictionary(
                    g => g.Key,
                    g => (IReadOnlyList<PathTier>)g
                        .OrderBy(p => p.PathOrdinal)
                        .Select(p => new PathTier(p.Id, p.PathOrdinal, p.MaxLevel))
                        .ToList());

            var corePaths = paths
                .Select(path => new CorePath
                {
                    Id = path.Id,
                    FalloffBase = (double)path.FalloffBase,
                    Tiers = tiersByPath.GetValueOrDefault(path.Id, []),
                })
                .ToList();

            // The reverse index the accrual consumes: each skill → its path contributions (path, home tier,
            // weight). The frontier routing and home-tier falloff are resolved at battle completion. Retired
            // paths are excluded here so retirement freezes the track at this single routing choke point: with
            // no contribution into it, a retired track accrues no XP, levels nothing, and grants no further
            // skills (already-accrued levels/grants are untouched — retirement only stops further accrual).
            var contributionsBySkill = paths
                .Where(path => !retiredPathIds.Contains(path.Id))
                .SelectMany(path => path.SkillContributions.Select(c => (c.SkillId, Contribution: new SkillContribution
                {
                    PathId = path.Id,
                    HomeTier = c.HomeTier,
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
                corePaths,
                contributionsBySkill);
        }
    }
}
