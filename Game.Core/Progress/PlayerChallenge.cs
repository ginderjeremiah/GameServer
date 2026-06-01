namespace Game.Core.Challenges
{
    /// <summary>
    /// Represents a player's progress toward completing a challenge.
    /// </summary>
    public class PlayerChallenge
    {
        public Challenge Challenge { get; private set; }
        public decimal Progress { get; private set; }
        public bool Completed { get; private set; }
        public DateTime? CompletedAt { get; private set; }

        public PlayerChallenge(Challenge challenge, decimal progress, bool completed, DateTime? completedAt = null)
        {
            Challenge = challenge;
            Progress = progress;
            Completed = completed;
            CompletedAt = completedAt;
        }

        public void UpdateProgress(decimal progress)
        {
            Progress = Math.Min(progress, Challenge.ProgressGoal);
            if (progress >= Challenge.ProgressGoal && !Completed)
            {
                Completed = true;
                CompletedAt = DateTime.UtcNow;
            }
        }
    }
}
