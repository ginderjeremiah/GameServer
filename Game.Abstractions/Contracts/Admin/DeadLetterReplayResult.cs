namespace Game.Abstractions.Contracts.Admin
{
    /// <summary>
    /// The outcome of a dead-letter replay: how many entries were redelivered (re-enqueued onto the player
    /// update queue, or re-dispatched to a live socket, #1542) and how many remain on the dead-letter queue
    /// afterward.
    /// </summary>
    public class DeadLetterReplayResult : IModel
    {
        /// <summary>The number of entries successfully redelivered.</summary>
        public int ReplayedCount { get; set; }

        /// <summary>The number of entries still on the dead-letter queue after the replay.</summary>
        public long RemainingCount { get; set; }
    }
}
