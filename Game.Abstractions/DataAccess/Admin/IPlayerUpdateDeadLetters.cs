using Game.Abstractions.Contracts.Admin;

namespace Game.Abstractions.DataAccess.Admin
{
    /// <summary>
    /// Guarded inspection and replay over the player write-behind dead-letter queue. Inspection is
    /// read-only (no destructive pop); replay moves entries back onto the player update queue and wakes the
    /// synchronizer, relying on the idempotent write-behind handlers so a re-applied event converges.
    /// </summary>
    public interface IPlayerUpdateDeadLetters
    {
        /// <summary>
        /// Reads the dead-letter queue's depth plus a head-first page of inspected entries (up to
        /// <paramref name="max"/>), classifying each so an operator can tell poison from replayable.
        /// </summary>
        Task<DeadLetterInspection> InspectAsync(int max);

        /// <summary>Replays every entry on the dead-letter queue back onto the player update queue.</summary>
        Task<DeadLetterReplayResult> ReplayAllAsync();

        /// <summary>
        /// Replays the entries identified by their exact <paramref name="payloads"/> back onto the player
        /// update queue. A payload that is not actually on the dead-letter queue is skipped, so the replay
        /// can never inject an arbitrary message onto the queue.
        /// </summary>
        Task<DeadLetterReplayResult> ReplaySelectedAsync(IReadOnlyList<string> payloads);
    }
}
