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
        StatisticsService statsService,
        ChallengeService challengeService,
        IPlayerRepository playerRepo)
        : IDomainEventHandler<EnemyDefeatedEvent>
    {
        private readonly StatisticsService _statsService = statsService;
        private readonly ChallengeService _challengeService = challengeService;
        private readonly IPlayerRepository _playerRepo = playerRepo;

        public async Task HandleAsync(EnemyDefeatedEvent domainEvent, CancellationToken cancellationToken = default)
        {
            var playerId = domainEvent.PlayerId;
            var enemyId = domainEvent.EnemyId;

            // Increment global enemies killed
            var totalKilled = await _statsService.RecordStatistic(
                playerId, EStatisticType.EnemiesKilled, 0, 1);

            // Increment per-enemy-type kill count
            var enemyTypeKilled = await _statsService.RecordStatistic(
                playerId, EStatisticType.EnemiesKilled, enemyId, 1);

            // Check challenges — load player to apply unlocks in-memory
            var player = await _playerRepo.GetPlayer(playerId);
            if (player is null) return;

            // Check global kill challenges
            await _challengeService.CheckAndUpdateProgress(
                player, EStatisticType.EnemiesKilled, 0, totalKilled);

            // Check per-enemy kill challenges
            await _challengeService.CheckAndUpdateProgress(
                player, EStatisticType.EnemiesKilled, enemyId, enemyTypeKilled);

            await _playerRepo.SavePlayer(player);
        }
    }
}
