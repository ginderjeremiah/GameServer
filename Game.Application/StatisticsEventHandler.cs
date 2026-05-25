using Game.Abstractions.DataAccess;
using Game.Application.Services;
using Game.Core;
using Game.Core.Events;

namespace Game.Application
{
    /// <summary>
    /// Handles <see cref="EnemyDefeatedEvent"/> by updating player statistics
    /// and checking challenge progress.
    /// </summary>
    public class StatisticsEventHandler(
        IPlayerStatistics playerStatistics,
        ChallengeService challengeService,
        IPlayerRepository playerRepo)
        : IDomainEventHandler<EnemyDefeatedEvent>
    {
        private readonly IPlayerStatistics _playerStatistics = playerStatistics;
        private readonly ChallengeService _challengeService = challengeService;
        private readonly IPlayerRepository _playerRepo = playerRepo;

        public async Task HandleAsync(EnemyDefeatedEvent domainEvent, CancellationToken cancellationToken = default)
        {
            var playerId = domainEvent.PlayerId;
            var enemyId = domainEvent.EnemyId;

            var totalKilled = await _playerStatistics.IncrementStatistic(
                playerId, (int)EStatisticType.EnemiesKilled, 0, 1);

            var enemyTypeKilled = await _playerStatistics.IncrementStatistic(
                playerId, (int)EStatisticType.EnemiesKilled, enemyId, 1);

            var player = await _playerRepo.GetPlayer(playerId);
            if (player is null) return;

            await _challengeService.CheckAndUpdateProgress(
                player, EStatisticType.EnemiesKilled, 0, totalKilled);

            await _challengeService.CheckAndUpdateProgress(
                player, EStatisticType.EnemiesKilled, enemyId, enemyTypeKilled);

            await _playerRepo.SavePlayer(player);
        }
    }
}
