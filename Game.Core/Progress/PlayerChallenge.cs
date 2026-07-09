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
        /// data: with none, neither <see cref="Progress"/> nor completion is touched (the 0 placeholder
        /// <paramref name="value"/> is not a real best), while a genuine 0 (<paramref name="hasData"/> true)
        /// can satisfy the goal.
        /// </param>
        public void UpdateProgress(decimal value, bool hasData, DateTime timestamp)
        {
            if (Completed)
            {
                return;
            }

            if (Challenge.Type.GoalComparison is EChallengeGoalComparison.AtMost)
            {
                // "At most" goals (e.g. time trials) are met by reaching a value at or below the goal.
                // With no data the 0 placeholder is not a real best, so Progress is left untouched —
                // storing it would surface a misleading 0 (the best possible) to the client.
                if (hasData)
                {
                    Progress = value;
                    if (value <= Challenge.ProgressGoal)
                    {
                        Completed = true;
                        CompletedAt = timestamp;
                    }
                }
            }
            else
            {
                Progress = Math.Min(value, Challenge.ProgressGoal);
                if (value >= Challenge.ProgressGoal)
                {
                    Completed = true;
                    CompletedAt = timestamp;
                }
            }
        }
    }
}
