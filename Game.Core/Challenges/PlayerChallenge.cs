namespace Game.Core.Challenges
{
    /// <summary>
    /// Represents a player's progress toward completing a challenge.
    /// </summary>
    public class PlayerChallenge
    {
        public required int ChallengeId { get; set; }
        public required decimal Progress { get; set; }
        public required decimal ProgressGoal { get; set; }
        public required bool Completed { get; set; }
        public DateTime? CompletedAt { get; set; }
    }
}
