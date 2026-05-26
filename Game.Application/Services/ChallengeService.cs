using Game.Abstractions.DataAccess;
using Game.Core;
using Game.Core.Challenges;
using Game.Core.Players;

namespace Game.Application.Services
{
    public class ChallengeService(
        IChallenges challengeRepo,
        IPlayerChallenges playerChallengeRepo)
    {
        private readonly IChallenges _challengeRepo = challengeRepo;
        private readonly IPlayerChallenges _playerChallengeRepo = playerChallengeRepo;

        /// <summary>
        /// Checks all challenges of the given type and updates progress for the player.
        /// Unlocks rewards on the domain object (caller is responsible for SavePlayer).
        /// </summary>
        public async Task<List<CompletedChallengeInfo>> CheckAndUpdateProgress(
            Player player,
            EStatisticType statType,
            int? entityId,
            long newValue)
        {
            var completed = new List<CompletedChallengeInfo>();
            var allChallenges = _challengeRepo.All();
            var playerChallenges = await _playerChallengeRepo.GetPlayerChallenges(player.Id);
            var completedSet = new HashSet<int>(playerChallenges.Where(pc => pc.Completed).Select(pc => pc.ChallengeId));

            var matchingChallengeType = statType switch
            {
                EStatisticType.EnemiesKilled => EChallengeType.KillCount,
                EStatisticType.BossesDefeated => EChallengeType.BossDefeat,
                EStatisticType.ZonesCleared => EChallengeType.ZoneClear,
                _ => (EChallengeType?)null,
            };

            if (matchingChallengeType is null)
                return completed;

            foreach (var challenge in allChallenges)
            {
                if ((EChallengeType)challenge.ChallengeTypeId != matchingChallengeType.Value)
                    continue;

                if (completedSet.Contains(challenge.Id))
                    continue;

                if (challenge.TargetEntityId.HasValue && challenge.TargetEntityId.Value != entityId)
                    continue;

                var progress = (int)Math.Min(newValue, challenge.TargetCount);
                await _playerChallengeRepo.UpdateProgress(player.Id, challenge.Id, progress);

                if (newValue >= challenge.TargetCount)
                {
                    await _playerChallengeRepo.CompleteChallenge(player.Id, challenge.Id);

                    if (challenge.RewardItemId.HasValue)
                    {
                        player.UnlockItem(challenge.RewardItemId.Value);
                    }

                    if (challenge.RewardItemModId.HasValue)
                    {
                        player.UnlockMod(challenge.RewardItemModId.Value);
                    }

                    completed.Add(new CompletedChallengeInfo
                    {
                        ChallengeId = challenge.Id,
                        RewardItemId = challenge.RewardItemId,
                        RewardItemModId = challenge.RewardItemModId,
                    });
                }
            }

            return completed;
        }
    }

    public class CompletedChallengeInfo
    {
        public required int ChallengeId { get; set; }
        public int? RewardItemId { get; set; }
        public int? RewardItemModId { get; set; }
    }
}
