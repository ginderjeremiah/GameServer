namespace Game.Core.Progress
{
    /// <summary>
    /// Represents a challenge that can be completed to unlock an item, modifier, and/or skill.
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
        public int? RewardSkillId { get; set; }

        /// <summary>When set, the challenge is retired: out of circulation for new authoring but kept
        /// resolvable by id so existing references (and completions) stay valid. Null while active.</summary>
        public DateTime? RetiredAt { get; set; }

        public void UpdateChallengeProgress(PlayerChallenge playerChallenge, PlayerProgress playerProgress)
        {
            if (Type.StatisticType is not null)
            {
                var hasData = playerProgress.TryGetStatisticValue(Type.StatisticType.Id, TargetEntityId, out var value);
                playerChallenge.UpdateProgress(value, hasData);
            }
            else if (Type.Id is EChallengeType.LevelReached)
            {
                // The player's level is always present, so the progress value is always real data.
                playerChallenge.UpdateProgress(playerProgress.Player.Level, hasData: true);
            }
        }
    }
}
