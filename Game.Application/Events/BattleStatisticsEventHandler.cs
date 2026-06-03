using Game.Abstractions.DataAccess;
using Game.Core.Battle.Events;
using Game.Core.Events;

namespace Game.Application.Events
{
    public class BattleStatisticsEventHandler(
        IPlayerProgressRepository progressRepo,
        IChallenges challengeRepo,
        IItems items
    ) : IDomainEventHandler<BattleCompletedEvent>
    {
        private readonly IPlayerProgressRepository _progressRepo = progressRepo;
        private readonly IChallenges _challengeRepo = challengeRepo;
        private readonly IItems _items = items;

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
                        var item = _items.GetItem(c.RewardItemId.Value);
                        player.UnlockItem(item);
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
