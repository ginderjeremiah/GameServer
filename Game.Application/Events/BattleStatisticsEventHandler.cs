using Game.Abstractions.DataAccess;
using Game.Application.Services;
using Game.Core.Battle.Events;
using Game.Core.Events;

namespace Game.Application.Events
{
    public class BattleStatisticsEventHandler(
        IPlayerProgressRepository progressRepo,
        ChallengeRewardService challengeRewards,
        ProficiencyRewardService proficiencyRewards
    ) : IDomainEventHandler<BattleCompletedEvent>
    {
        private readonly IPlayerProgressRepository _progressRepo = progressRepo;
        private readonly ChallengeRewardService _challengeRewards = challengeRewards;
        private readonly ProficiencyRewardService _proficiencyRewards = proficiencyRewards;

        public async Task HandleAsync(BattleCompletedEvent domainEvent, CancellationToken cancellationToken = default)
        {
            // Progress is loaded per battle (a cache-first, typically warm read) rather than co-located in
            // memory with the player — a deliberate trade-off of the separate write-behind key. See
            // docs/backend-persistence.md (write-behind player cache) for why co-location is rejected.
            var progress = await _progressRepo.Load(domainEvent.Player, cancellationToken);

            var touchedStatistics = progress.RecordBattleCompleted(
                domainEvent.Enemy, domainEvent.Victory, domainEvent.PlayerDied, domainEvent.TotalMs,
                domainEvent.Stats, domainEvent.IsBossBattle, domainEvent.ZoneId);

            // Evaluate only the challenges whose tracked statistic this battle actually moved (plus the
            // statistic-independent ones) and apply their rewards, raising the live per-challenge push. The
            // offline-rewards batch runs this same step with the push suppressed; a live battle whose completion
            // was instead settled by the offline/switch stale-battle resolution (BattleService.ResolveStaleBattle)
            // suppresses it here too, via domainEvent.Notify, since that settlement has no socket to push to.
            _challengeRewards.EvaluateAndApply(
                progress, touchedStatistics, domainEvent.Player, DateTime.UtcNow, notify: domainEvent.Notify);

            // Accrue proficiency XP on a victory: each path claims pie × activity ÷ max(playerRating,
            // enemyRating), routed to its frontier tier (the effect-based model, spike #1318, max-normalized per
            // spike #1526 Decision 5). Raises the live per-battle push; the offline batch (and the stale-battle
            // settlement above) runs the same accrual with it suppressed.
            if (domainEvent.Victory)
            {
                var ratingDenominator = Math.Max(domainEvent.PlayerRating, domainEvent.EnemyRating);
                _proficiencyRewards.AccrueAndApply(
                    progress, domainEvent.Stats, ratingDenominator, domainEvent.Player, notify: domainEvent.Notify);
            }

            await _progressRepo.Save(progress, cancellationToken);
        }
    }
}
