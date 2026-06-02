using Game.Abstractions.DataAccess;
using Game.Core.Battle.Events;
using Game.Core.Events;

namespace Game.Application.Events
{
    public class BattleStatisticsEventHandler(
        IPlayerProgressRepository progressRepo,
        IChallenges challengeRepo
    ) : IDomainEventHandler<BattleCompletedEvent>
    {
        private readonly IPlayerProgressRepository _progressRepo = progressRepo;
        private readonly IChallenges _challengeRepo = challengeRepo;

        public async Task HandleAsync(BattleCompletedEvent domainEvent, CancellationToken cancellationToken = default)
        {
            var progress = await _progressRepo.Load(domainEvent.Player);

            progress.RecordBattleCompleted(domainEvent.Enemy, domainEvent.Victory, domainEvent.PlayerDied, domainEvent.TotalMs, domainEvent.Stats);

            var completed = progress.EvaluateChallenges(_challengeRepo.All());

            if (completed.Count > 0)
            {
                var player = domainEvent.Player;
                foreach (var c in completed)
                {
                    if (c.RewardItemId.HasValue)
                    {
                        player.UnlockItem(c.RewardItemId.Value);
                    }
                    if (c.RewardItemModId.HasValue)
                    {
                        player.UnlockMod(c.RewardItemModId.Value);
                    }
                }
            }

            _progressRepo.Save(progress);
        }
    }
}
