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

        /// <param name="hasData">
        /// Whether the tracked statistic has a recorded value. "At most" goals only count when there is
        /// data: with none, the goal is unmet regardless of the 0 placeholder <paramref name="value"/>,
        /// while a genuine 0 (<paramref name="hasData"/> true) can satisfy it.
        /// </param>
        public void UpdateProgress(decimal value, bool hasData)
        {
            if (Completed)
            {
                return;
            }

            if (Challenge.Type.GoalComparison is EChallengeGoalComparison.AtMost)
            {
                // "At most" goals (e.g. time trials) are met by reaching a value at or below the goal.
                // Completion keys off whether the statistic has data, never off a magic 0 — see hasData.
                Progress = value;
                if (hasData && value <= Challenge.ProgressGoal)
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
