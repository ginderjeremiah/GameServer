namespace Game.Abstractions.Contracts.Admin
{
    /// <summary>
    /// A single entry inspected from a dead-letter queue — the player write-behind queue or the socket
    /// command queue (#1542), which share this shape: where it sits, what it is, who it belongs to, why it
    /// failed, and the exact stored payload (the identity used to replay it).
    /// </summary>
    public class DeadLetterEntry
    {
        /// <summary>Zero-based position from the head of the queue (0 = oldest), for display only.</summary>
        public int Index { get; set; }

        /// <summary>
        /// The domain event's type name from the envelope (player queue), or the server-initiated command's
        /// type name (socket queue); null when the payload is malformed.
        /// </summary>
        public string? EventType { get; set; }

        /// <summary>The owning/addressed player id parsed from the payload, when derivable.</summary>
        public int? PlayerId { get; set; }

        /// <summary>Why the entry was dead-lettered.</summary>
        public EDeadLetterReason Reason { get; set; }

        /// <summary>The exact raw message as stored on the queue — pass it back verbatim to replay it.</summary>
        public required string RawPayload { get; set; }
    }
}
