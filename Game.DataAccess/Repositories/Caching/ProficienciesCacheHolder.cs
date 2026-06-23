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
        IReadOnlyDictionary<int, IReadOnlyList<SkillContribution>> ContributionsBySkill,
        IReadOnlyDictionary<int, IReadOnlyList<int>> DependentsByProficiency);

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

            // Build-time invariant: the authored prerequisite graph must be acyclic, since a cycle would
            // soft-lock every node on it under the "open once prerequisites are maxed" rule. The admin save
            // rejects a cycle before it commits; this is the backstop against a seed/migration mistake (it
            // fails the build-then-swap, keeping the prior good snapshot or surfacing as a boot failure).
            var prerequisiteGraph = entities.ToDictionary(
                p => p.Id,
                p => (IReadOnlyList<int>)p.Prerequisites.Select(pr => pr.PrerequisiteProficiencyId).ToList());
            if (ProficiencyPrerequisiteGraph.TryFindCycle(prerequisiteGraph, out var cycle))
            {
                throw new InvalidOperationException(
                    $"Proficiency prerequisite graph contains a cycle: {string.Join(" -> ", cycle)}.");
            }

            // The reverse prerequisite index the open logic consumes: each proficiency → the proficiencies that
            // gate on it, so maxing a node can resolve the gateways it might open without rescanning the set.
            var dependentsByProficiency = entities
                .SelectMany(p => p.Prerequisites.Select(pr => (Gated: p.Id, Prerequisite: pr.PrerequisiteProficiencyId)))
                .GroupBy(x => x.Prerequisite)
                .ToDictionary(
                    g => g.Key,
                    g => (IReadOnlyList<int>)g.Select(x => x.Gated).OrderBy(id => id).ToList());

            var paths = await context.Paths
                .AsNoTracking()
                .Include(p => p.SkillContributions)
                .OrderBy(p => p.Id)
                .ToListAsync(cancellationToken);

            paths.AssertZeroBasedContiguity("Paths");

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
            // weight). The frontier routing and home-tier falloff are resolved at battle completion.
            var contributionsBySkill = paths
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
                contributionsBySkill,
                dependentsByProficiency);
        }
    }
}
