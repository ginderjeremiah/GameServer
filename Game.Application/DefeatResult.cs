namespace Game.Application
{
    /// <summary>
    /// Carries the outcome of a successful enemy defeat.
    /// </summary>
    public class DefeatResult
    {
        public required int ExpReward { get; set; }
        public required int NewLevel { get; set; }
        public required int NewExp { get; set; }
        public required int StatPointsGained { get; set; }
        public required int StatPointsUsed { get; set; }

        /// <summary>
        /// The player's combat-rating capability measure for this battle (<see cref="Game.Core.Battle.DefeatRewards.PlayerRating"/>,
        /// spike #1526 Decision 7) — carried through so the client's displayed power number reflects this
        /// battle's outcome without waiting for the next player-state refresh. This is the rating of the
        /// frozen snapshot the battle actually ran against (the same anti-tamper measure the reward used),
        /// so a level-up this battle granted lags by one battle — it shows up once the following battle's
        /// snapshot is rated, not on this response.
        /// </summary>
        public required double PlayerRating { get; set; }
    }
}
