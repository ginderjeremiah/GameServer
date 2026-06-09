using Game.Abstractions.DataAccess;
using Game.Core.Battle.Events;
using Game.Core.Events;

namespace Game.Application.Events
{
    public class BattleStatisticsEventHandler(
        IPlayerProgressRepository progressRepo,
        IChallenges challengeRepo,
        IItems items,
        ISkills skills
    ) : IDomainEventHandler<BattleCompletedEvent>
    {
        private readonly IPlayerProgressRepository _progressRepo = progressRepo;
        private readonly IChallenges _challengeRepo = challengeRepo;
        private readonly IItems _items = items;
        private readonly ISkills _skills = skills;

        public async Task HandleAsync(BattleCompletedEvent domainEvent, CancellationToken cancellationToken = default)
        {
            var progress = await _progressRepo.Load(domainEvent.Player);

            progress.RecordBattleCompleted(
                domainEvent.Enemy, domainEvent.Victory, domainEvent.PlayerDied, domainEvent.TotalMs,
                domainEvent.Stats, domainEvent.IsBossBattle, domainEvent.ZoneId);

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
                    if (c.RewardSkillId.HasValue)
                    {
                        var skill = _skills.GetSkill(c.RewardSkillId.Value);
                        player.UnlockSkill(skill);
                    }
                }
            }

            await _progressRepo.Save(progress);
        }
    }
}
