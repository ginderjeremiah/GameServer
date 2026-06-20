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
            // Progress is loaded per battle (a cache-first, typically warm Redis read) rather than co-located
            // in memory with the player for the connection's lifetime. That is deliberate: progress is a
            // separate write-behind aggregate under its own key precisely to keep it off the player blob, which
            // is re-serialized in full on every save and would otherwise grow with the stat set (see
            // docs/backend-persistence.md). Co-locating it would either fold it back into that blob (the bloat
            // the split avoids) or require a connection-scoped holder reachable from this application-layer
            // handler — which only sees the player aggregate, not the presentation-layer session — so the
            // per-battle load is accepted over inverting that dependency to save one warm cache read.
            var progress = await _progressRepo.Load(domainEvent.Player, cancellationToken);

            var touchedStatistics = progress.RecordBattleCompleted(
                domainEvent.Enemy, domainEvent.Victory, domainEvent.PlayerDied, domainEvent.TotalMs,
                domainEvent.Stats, domainEvent.IsBossBattle, domainEvent.ZoneId);

            // Evaluate only the challenges whose tracked statistic this battle actually moved (plus the
            // statistic-independent ones), rather than re-scanning the whole authored catalog each battle.
            var relevantChallenges = _challengeRepo.Index().RelevantTo(touchedStatistics);
            var completed = progress.EvaluateChallenges(relevantChallenges);

            if (completed.Count > 0)
            {
                var player = domainEvent.Player;
                foreach (var c in completed)
                {
                    // Resolve the reward reference data here (the data-access concern) and let the domain
                    // own the rest: unlocking each reward and raising the ChallengeCompletedEvent.
                    var rewardItem = c.RewardItemId.HasValue ? _items.GetItem(c.RewardItemId.Value) : null;
                    var rewardSkill = c.RewardSkillId.HasValue ? _skills.GetSkill(c.RewardSkillId.Value) : null;
                    player.CompleteChallenge(c.ChallengeId, rewardItem, c.RewardItemModId, rewardSkill);
                }
            }

            await _progressRepo.Save(progress, cancellationToken);
        }
    }
}
