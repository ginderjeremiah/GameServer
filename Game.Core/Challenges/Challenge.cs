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
        public required EChallengeType Type { get; set; }
        public EStatisticType? StatisticType { get; set; }
        public EEntityType EntityType { get; set; }
        public int? TargetEntityId { get; set; }
        public required decimal ProgressGoal { get; set; }
        public int? RewardItemId { get; set; }
        public int? RewardItemModId { get; set; }
    }
}
