using Game.Core;
using Game.Core.Progress;
using Game.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Game.DataAccess.Repositories.Caching
{
    /// <summary>
    /// An immutable snapshot of the challenge reference set: the ordered challenge list (the existing
    /// <see cref="Abstractions.DataAccess.IChallenges"/> read shape) plus the derived
    /// <see cref="ChallengeIndex"/> that maps the statistic a challenge tracks to the challenges tracking
    /// it. Both are built and published together — like <see cref="EnemySnapshot"/> — so a reader can never
    /// observe a new challenge list against a stale (or null) index.
    /// </summary>
    internal sealed record ChallengeSnapshot(
        IReadOnlyList<Challenge> Challenges,
        ChallengeIndex Index);

    /// <summary>
    /// Singleton snapshot holder for the cached challenge list and its derived reverse index. Unlike the
    /// other reference sets the cache holds the gameplay domain <see cref="Challenge"/> (projected in an
    /// EF-translatable query) rather than the EF entity, matching the existing
    /// <see cref="Abstractions.DataAccess.IChallenges"/> read shape.
    /// </summary>
    internal sealed class ChallengesCacheHolder(IServiceScopeFactory scopeFactory)
        : ReferenceCacheHolder<ChallengeSnapshot>(scopeFactory)
    {
        protected override async Task<ChallengeSnapshot> BuildSnapshotAsync(GameContext context, CancellationToken cancellationToken)
        {
            var challenges = await context.Challenges
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

            return new ChallengeSnapshot(challenges, new ChallengeIndex(challenges));
        }
    }
}
