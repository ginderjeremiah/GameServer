using Game.Core;
using Game.DataAccess.Mapping;
using Game.Infrastructure.Database;
using Game.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using CorePath = Game.Core.Proficiencies.Path;
using CoreProficiency = Game.Core.Proficiencies.Proficiency;
using Path = Game.Infrastructure.Entities.Path;
using PathTier = Game.Core.Proficiencies.PathTier;
using ProficiencyPrerequisiteGraph = Game.Core.Proficiencies.ProficiencyPrerequisiteGraph;

namespace Game.DataAccess.Repositories.Caching
{
    /// <summary>
    /// An immutable snapshot of the proficiency reference set: the ordered proficiency and path entity lists
    /// (contract projection and admin entity lookups), the pre-materialized lean <see cref="CoreProficiency"/>
    /// domain models, the <see cref="CorePath"/> routing models, and the derived activity-key → paths reverse
    /// index. All are built and published together so a reader can never observe a new entity list against a
    /// stale index.
    /// <para>
    /// Each path declares one <see cref="EActivityKey"/>; the reverse index resolves a key to the (non-retired)
    /// paths that train on it, so the battle XP accrual can route a battle quantity to each path's frontier tier
    /// at completion against the <see cref="CorePath"/> models.
    /// </para>
    /// </summary>
    internal sealed record ProficiencySnapshot(
        IReadOnlyList<Proficiency> Entities,
        IReadOnlyList<Path> Paths,
        IReadOnlyList<CoreProficiency> CoreProficiencies,
        IReadOnlyList<CorePath> CorePaths,
        IReadOnlyDictionary<EActivityKey, IReadOnlyList<CorePath>> PathsByActivityKey,
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

            var paths = await context.Paths
                .AsNoTracking()
                .OrderBy(p => p.Id)
                .ToListAsync(cancellationToken);

            paths.AssertZeroBasedContiguity("Paths");

            var retiredPathIds = paths
                .Where(path => path.RetiredAt is not null)
                .Select(path => path.Id)
                .ToHashSet();

            // Build-time invariant: the authored prerequisite graph, combined with the implicit within-path tier
            // ordering, must be acyclic, since a cycle would soft-lock every node on it under the "open once
            // prerequisites are maxed" rule. The admin save rejects a cycle before it commits; this is the
            // backstop against a seed/migration mistake (it fails the build-then-swap, keeping the prior good
            // snapshot or surfacing as a boot failure).
            var prerequisites = entities.ToDictionary(
                p => p.Id,
                p => (IReadOnlyList<int>)p.Prerequisites.Select(pr => pr.PrerequisiteProficiencyId).ToList());
            var tiers = entities.Select(p => (p.Id, p.PathId, p.PathOrdinal)).ToList();
            var prerequisiteGraph = ProficiencyPrerequisiteGraph.BuildGraph(tiers, prerequisites);
            if (ProficiencyPrerequisiteGraph.TryFindCycle(prerequisiteGraph, out var cycle))
            {
                throw new InvalidOperationException(
                    $"Proficiency prerequisite graph contains a cycle: {string.Join(" -> ", cycle)}.");
            }

            // The reverse prerequisite index the open logic consumes: each proficiency → the proficiencies that
            // gate on it, so maxing a node can resolve the gateways it might open without rescanning the set.
            // Proficiencies on a retired path are excluded from the gated side: a retired track is frozen (see
            // pathsByActivityKey below), so it must never be opened as a cross-path gateway when its live
            // prerequisites max — mirroring the accrual freeze at the open choke point.
            var dependentsByProficiency = entities
                .Where(p => !retiredPathIds.Contains(p.PathId))
                .SelectMany(p => p.Prerequisites.Select(pr => (Gated: p.Id, Prerequisite: pr.PrerequisiteProficiencyId)))
                .GroupBy(x => x.Prerequisite)
                .ToDictionary(
                    g => g.Key,
                    g => (IReadOnlyList<int>)g.Select(x => x.Gated).OrderBy(id => id).ToList());

            // The routing models: each path's activity key plus its tiers ordered by ordinal (the proficiencies
            // carrying its id), so the accrual can resolve the frontier tier off the player's levels. Built in
            // path-id order so the list is index == id for GetById, matching the entity list.
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
                    ActivityKey = (EActivityKey)path.ActivityKey,
                    Tiers = tiersByPath.GetValueOrDefault(path.Id, []),
                })
                .ToList();

            // The reverse index the accrual consumes: each activity key → the paths that train on it. The
            // frontier routing is resolved at battle completion against these CorePath models. Retired paths are
            // excluded so retirement freezes the track at this single routing choke point: absent from the
            // index, a retired track accrues no XP, levels nothing, and grants no further skills (already-accrued
            // levels/grants are untouched — retirement only stops further accrual).
            var pathsByActivityKey = corePaths
                .Where(path => !retiredPathIds.Contains(path.Id))
                .GroupBy(path => path.ActivityKey)
                .ToDictionary(
                    g => g.Key,
                    g => (IReadOnlyList<CorePath>)g.ToList());

            return new ProficiencySnapshot(
                entities,
                paths,
                entities.Select(ProficiencyMapper.ToCore).ToList(),
                corePaths,
                pathsByActivityKey,
                dependentsByProficiency);
        }
    }
}
