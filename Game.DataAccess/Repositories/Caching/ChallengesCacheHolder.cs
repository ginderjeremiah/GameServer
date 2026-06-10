using Game.Core;
using Game.Core.Progress;
using Game.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Game.DataAccess.Repositories.Caching
{
    /// <summary>
    /// Singleton snapshot holder for the cached challenge list. Unlike the other reference sets the cache
    /// holds the gameplay domain <see cref="Challenge"/> (projected in an EF-translatable query) rather than
    /// the EF entity, matching the existing <see cref="Abstractions.DataAccess.IChallenges"/> read shape.
    /// </summary>
    internal sealed class ChallengesCacheHolder(IServiceScopeFactory scopeFactory)
        : ReferenceCacheHolder<IReadOnlyList<Challenge>>(scopeFactory)
    {
        protected override async Task<IReadOnlyList<Challenge>> BuildSnapshotAsync(GameContext context, CancellationToken cancellationToken)
        {
            return await context.Challenges
                .AsNoTracking()
                .OrderBy(c => c.Id)
                .Select(c => new Challenge
                {
                    Id = c.Id,
                    Name = c.Name,
                    Description = c.Description,
                    Type = new ChallengeType((EChallengeType)c.ChallengeTypeId),
                    TargetEntityId = c.TargetEntityId,
                    ProgressGoal = c.ProgressGoal,
                    RewardItemId = c.RewardItemId,
                    RewardItemModId = c.RewardItemModId,
                    RewardSkillId = c.RewardSkillId,
                    RetiredAt = c.RetiredAt,
                })
                .ToListAsync(cancellationToken);
        }
    }
}
