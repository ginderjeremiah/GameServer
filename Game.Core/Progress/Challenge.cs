using Game.Core.Progress;

namespace Game.Core.Challenges
{
    /// <summary>
    /// Represents a challenge that can be completed to unlock an item or modifier.
    /// </summary>
    public class Challenge
    {
        public required int Id { get; set; }
        public required string Name { get; set; }
        public required string Description { get; set; }
        public required ChallengeType Type { get; set; }
        public int? TargetEntityId { get; set; }
        public required decimal ProgressGoal { get; set; }
        public int? RewardItemId { get; set; }
        public int? RewardItemModId { get; set; }

        public void UpdateChallengeProgress(PlayerChallenge playerChallenge, PlayerProgress playerProgress)
        {
            if (Type.StatisticType is not null)
            {
                var value = playerProgress.GetStatisticValue(Type.StatisticType.Id, TargetEntityId);
                playerChallenge.UpdateProgress(value);
            }
            else if (Type.Id is EChallengeType.LevelReached)
            {
                var value = playerProgress.Player.Level;
                playerChallenge.UpdateProgress(value);
            }
        }
    }
}
