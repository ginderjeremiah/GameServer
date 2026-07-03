using Game.DataAccess.Mapping;
using Game.Infrastructure.Database;
using Game.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using CoreSkillRecipe = Game.Core.Skills.SkillRecipe;

namespace Game.DataAccess.Repositories.Caching
{
    /// <summary>
    /// An immutable snapshot of the skill-recipe reference set (spike #1125): the ordered recipe entity list
    /// (contract projection and admin entity lookups) and the pre-materialized lean <see cref="CoreSkillRecipe"/>
    /// domain models, built and published together so a reader can never observe a torn pair.
    /// </summary>
    internal sealed record SkillRecipeSnapshot(
        IReadOnlyList<SkillRecipe> Entities,
        IReadOnlyList<CoreSkillRecipe> CoreRecipes);

    /// <summary>Singleton snapshot holder for the cached skill-recipe entity list and its derived structures.</summary>
    internal sealed class SkillRecipesCacheHolder(IServiceScopeFactory scopeFactory)
        : ReferenceCacheHolder<SkillRecipeSnapshot>(scopeFactory)
    {
        protected override async Task<SkillRecipeSnapshot> BuildSnapshotAsync(GameContext context, CancellationToken cancellationToken)
        {
            var entities = await context.SkillRecipes
                .AsNoTracking()
                .Include(r => r.Inputs)
                .Include(r => r.Conditions)
                .AsSplitQuery()
                .OrderBy(r => r.Id)
                .ToListAsync(cancellationToken);

            entities.AssertZeroBasedContiguity("SkillRecipes");

            // Build-time invariant: the authored recipe graph must be acyclic, since a cycle would make a skill
            // its own ancestor (unreachable). The admin save rejects a cycle before it commits; this is the
            // backstop against a seed/migration mistake (it fails the build-then-swap, keeping the prior good
            // snapshot or surfacing as a boot failure). Retired recipes are inert, so they are excluded.
            var dependencyGraph = SkillRecipeGraph.BuildDependencyGraph(entities
                .Where(r => r.RetiredAt is null)
                .Select(r => (r.ResultSkillId, (IReadOnlyList<int>)r.Inputs.Select(i => i.SkillId).ToList())));
            if (SkillRecipeGraph.TryFindCycle(dependencyGraph, out var cycle))
            {
                throw new InvalidOperationException(
                    $"Skill recipe graph contains a cycle (a skill cannot be synthesized from itself): {string.Join(" -> ", cycle)}.");
            }

            return new SkillRecipeSnapshot(
                entities,
                entities.Select(SkillRecipeMapper.ToCore).ToList());
        }
    }
}
