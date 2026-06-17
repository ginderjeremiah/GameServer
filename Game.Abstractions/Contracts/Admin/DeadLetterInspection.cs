namespace Game.Abstractions.Contracts.Admin
{
    /// <summary>
    /// A read-only snapshot of the player write-behind dead-letter queue: its full depth plus a head-first
    /// page of inspected entries (capped by the requested limit). Inspecting never removes anything.
    /// </summary>
    public class DeadLetterInspection : IModel
    {
        /// <summary>The total number of messages on the dead-letter queue (may exceed <see cref="Entries"/>.Count).</summary>
        public long TotalCount { get; set; }

        /// <summary>The inspected entries, oldest first, up to the requested limit.</summary>
        public required IReadOnlyList<DeadLetterEntry> Entries { get; set; }
    }
}
