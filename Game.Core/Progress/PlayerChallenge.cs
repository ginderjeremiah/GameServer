namespace Game.Core.Progress
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

        public void UpdateProgress(decimal value)
        {
            if (Completed)
            {
                return;
            }

            if (Challenge.Type.GoalComparison is EChallengeGoalComparison.AtMost)
            {
                // "At most" goals (e.g. time trials) are satisfied by reaching a value at or below
                // the goal. A value of 0 indicates the underlying statistic has no data yet (e.g. no
                // victory has been recorded), so it does not count as meeting the goal.
                Progress = value;
                if (value > 0 && value <= Challenge.ProgressGoal)
                {
                    Completed = true;
                    CompletedAt = DateTime.UtcNow;
                }
            }
            else
            {
                Progress = Math.Min(value, Challenge.ProgressGoal);
                if (value >= Challenge.ProgressGoal)
                {
                    Completed = true;
                    CompletedAt = DateTime.UtcNow;
                }
            }
        }
    }
}
