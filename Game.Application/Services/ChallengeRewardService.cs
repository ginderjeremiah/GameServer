using Game.Abstractions.DataAccess;
using Game.Core;
using Game.Core.Players;
using Game.Core.Progress;

namespace Game.Application.Services
{
    /// <summary>
    /// The shared challenge-evaluation + reward-application step: given a player's progress and the statistics
    /// a batch of battles touched, it evaluates the relevant challenges, unlocks each completion's rewards on
    /// the player, and returns the completions. Both the live battle-completion handler and the offline-rewards
    /// batch run it, so the "evaluate the relevant challenges, resolve their rewards, complete them" logic lives
    /// in one place rather than being duplicated (spike #879 decision 7).
    /// <para>
    /// It resolves the reward reference data (the data-access concern) and delegates the unlock + completion
    /// event to the <see cref="Player"/> aggregate. The <c>notify</c> flag is threaded straight to
    /// <see cref="Player.CompleteChallenge"/>: the live path notifies (a per-challenge client push), the
    /// offline batch suppresses it (the welcome-back summary is the notification).
    /// </para>
    /// </summary>
    public class ChallengeRewardService(IChallenges challenges, IItems items)
    {
        private readonly IChallenges _challenges = challenges;
        private readonly IItems _items = items;

        /// <summary>
        /// Evaluates the challenges relevant to <paramref name="touchedStatistics"/> against
        /// <paramref name="progress"/> (plus the statistic-independent ones), applies each completion's rewards
        /// to <paramref name="player"/>, and returns the completions so the caller can surface them. Pass
        /// <paramref name="notify"/> <c>true</c> on the live path to raise the per-challenge client push, or
        /// <c>false</c> for the offline batch to suppress it. <paramref name="timestamp"/> stamps any completion's
        /// <see cref="PlayerChallenge.CompletedAt"/> — the live "now" or, for the offline batch, the simulated
        /// away-window time rather than the moment the rewards are claimed.
        /// </summary>
        public IReadOnlyList<CompletedChallenge> EvaluateAndApply(
            PlayerProgress progress,
            IReadOnlyCollection<(EStatisticType Type, int? EntityId)> touchedStatistics,
            Player player,
            DateTime timestamp,
            bool notify)
        {
            var relevantChallenges = _challenges.Index().RelevantTo(touchedStatistics);
            var completed = progress.EvaluateChallenges(relevantChallenges, timestamp);

            foreach (var c in completed)
            {
                // Resolve the reward reference data here (the data-access concern) and let the domain own the
                // rest: unlocking each reward and (when notifying) raising the ChallengeCompletedEvent.
                var rewardItem = c.RewardItemId.HasValue ? _items.GetItem(c.RewardItemId.Value) : null;
                player.CompleteChallenge(c.ChallengeId, rewardItem, c.RewardItemModId, notify);
            }

            return completed;
        }
    }
}
