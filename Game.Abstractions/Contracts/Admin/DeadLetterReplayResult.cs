namespace Game.Abstractions.Contracts.Admin
{
    /// <summary>
    /// The outcome of a dead-letter replay: how many entries were moved back onto the player update queue
    /// and how many remain on the dead-letter queue afterward.
    /// </summary>
    public class DeadLetterReplayResult : IModel
    {
        /// <summary>The number of entries re-enqueued onto the player update queue.</summary>
        public int ReplayedCount { get; set; }

        /// <summary>The number of entries still on the dead-letter queue after the replay.</summary>
        public long RemainingCount { get; set; }
    }
}
