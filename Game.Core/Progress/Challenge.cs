namespace Game.Core.Progress
{
    /// <summary>
    /// Represents a challenge that can be completed to unlock an item, modifier, and/or skill. Shared, cached
    /// reference-data instance: structurally immutable (init-only) so the cached snapshot handed to every
    /// battle's challenge evaluation cannot be corrupted (#547).
    /// </summary>
    public class Challenge
    {
        public required int Id { get; init; }
        public required string Name { get; init; }
        public required string Description { get; init; }
        public required ChallengeType Type { get; init; }
        public int? TargetEntityId { get; init; }
        public required decimal ProgressGoal { get; init; }
        public int? RewardItemId { get; init; }
        public int? RewardItemModId { get; init; }
        public int? RewardSkillId { get; init; }

        /// <summary>When set, the challenge is retired: out of circulation for new authoring but kept
        /// resolvable by id so existing references (and completions) stay valid. Null while active.</summary>
        public DateTime? RetiredAt { get; init; }

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
            else
            {
                // A challenge type with no backing statistic must be explicitly handled above (like
                // LevelReached). Falling through here means a new type was wired up only half-way and
                // would otherwise silently never progress — fail loud instead.
                throw new ArgumentOutOfRangeException(nameof(Type), Type.Id,
                    $"Challenge type {Type.Id} has no backing statistic and no progress handling.");
            }
        }
    }
}
