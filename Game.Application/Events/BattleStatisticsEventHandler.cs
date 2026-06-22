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
            // offline-rewards batch runs this same step with the push suppressed.
            _challengeRewards.EvaluateAndApply(progress, touchedStatistics, domainEvent.Player, notify: true);

            // Accrue proficiency XP on a victory: the fixed pie scaled by the battle's difficulty multiplier,
            // split across the proficiencies whose skills fired (spike #982). Raises the live per-battle push;
            // the offline batch runs the same accrual with it suppressed.
            if (domainEvent.Victory)
            {
                _proficiencyRewards.AccrueAndApply(
                    progress, domainEvent.Stats, domainEvent.DifficultyMultiplier, domainEvent.Player, notify: true);
            }

            await _progressRepo.Save(progress, cancellationToken);
        }
    }
}
