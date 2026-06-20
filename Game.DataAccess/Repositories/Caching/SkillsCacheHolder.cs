using Game.DataAccess.Mapping;
using Game.Infrastructure.Database;
using Game.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using CoreSkill = Game.Core.Skills.Skill;

namespace Game.DataAccess.Repositories.Caching
{
    /// <summary>
    /// An immutable snapshot of the skill reference set: the ordered entity list (used for the contract
    /// <c>AllSkills()</c> projection and the admin entity lookups) plus the
    /// pre-materialized lean <see cref="CoreSkill"/> domain models, built and published together so a reader
    /// can never observe a new entity list against stale core models. The shared <see cref="CoreSkill"/>
    /// instances are returned from <c>Skills.GetSkill</c> instead of rebuilding a fresh graph per call
    /// (see <see cref="ItemSnapshot"/>).
    /// </summary>
    internal sealed record SkillSnapshot(
        IReadOnlyList<Skill> Entities,
        IReadOnlyList<CoreSkill> CoreSkills);

    /// <summary>Singleton snapshot holder for the cached skill entity list and its derived core models.</summary>
    internal sealed class SkillsCacheHolder(IServiceScopeFactory scopeFactory)
        : ReferenceCacheHolder<SkillSnapshot>(scopeFactory)
    {
        protected override async Task<SkillSnapshot> BuildSnapshotAsync(GameContext context, CancellationToken cancellationToken)
        {
            var entities = await context.Skills
                .AsNoTracking()
                .Include(s => s.SkillDamageMultipliers)
                .Include(s => s.SkillEffects)
                .AsSplitQuery()
                .OrderBy(s => s.Id)
                .ToListAsync(cancellationToken);

            entities.AssertZeroBasedContiguity("Skills");

            return new SkillSnapshot(entities, entities.Select(SkillMapper.ToCore).ToList());
        }
    }
}
