using Game.DataAccess.Mapping;
using Game.Infrastructure.Database;
using Game.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using CoreProficiency = Game.Core.Proficiencies.Proficiency;
using SkillContribution = Game.Core.Proficiencies.SkillContribution;

namespace Game.DataAccess.Repositories.Caching
{
    /// <summary>
    /// An immutable snapshot of the proficiency reference set: the ordered entity list (contract projection
    /// and admin entity lookups), the pre-materialized lean <see cref="CoreProficiency"/> domain models, and
    /// the derived skill → contributions reverse index (the battle XP path looks proficiencies up by the
    /// skills that fired). All three are built and published together so a reader can never observe a new
    /// entity list against stale core models or a stale index.
    /// </summary>
    internal sealed record ProficiencySnapshot(
        IReadOnlyList<Proficiency> Entities,
        IReadOnlyList<CoreProficiency> CoreProficiencies,
        IReadOnlyDictionary<int, IReadOnlyList<SkillContribution>> ContributionsBySkill);

    /// <summary>Singleton snapshot holder for the cached proficiency entity list and its derived structures.</summary>
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
                .Include(p => p.SkillContributions)
                .AsSplitQuery()
                .OrderBy(p => p.Id)
                .ToListAsync(cancellationToken);

            entities.AssertZeroBasedContiguity("Proficiencies");

            var contributionsBySkill = entities
                .SelectMany(p => p.SkillContributions.Select(c => (c.SkillId, Contribution: new SkillContribution
                {
                    ProficiencyId = p.Id,
                    Weight = (double)c.Weight,
                })))
                .GroupBy(x => x.SkillId)
                .ToDictionary(
                    g => g.Key,
                    g => (IReadOnlyList<SkillContribution>)g.Select(x => x.Contribution).ToList());

            return new ProficiencySnapshot(
                entities,
                entities.Select(ProficiencyMapper.ToCore).ToList(),
                contributionsBySkill);
        }
    }
}
