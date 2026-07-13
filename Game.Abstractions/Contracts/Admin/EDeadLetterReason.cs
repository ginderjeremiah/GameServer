namespace Game.Abstractions.Contracts.Admin
{
    /// <summary>
    /// Why an entry is sitting on a dead-letter queue — the player write-behind event queue or the socket
    /// command queue (#1542). Surfaced by the inspection surface so an operator can tell a genuinely-poison
    /// entry (pointless to replay) from one worth replaying once its transient cause has cleared.
    /// </summary>
    public enum EDeadLetterReason
    {
        /// <summary>The stored message could not be parsed into its envelope (malformed or empty);
        /// replaying it will just re-fail and re-dead-letter.</summary>
        Malformed = 0,

        /// <summary>The envelope parsed but names a type nothing on the consuming side handles;
        /// replaying it will just re-fail and re-dead-letter.</summary>
        UnknownEventType = 1,

        /// <summary>A well-formed, known entry that exhausted its retries (or delivery attempts) on a
        /// transient failure; worth replaying once the underlying cause is fixed.</summary>
        Replayable = 2,

        /// <summary>A well-formed, known entry whose command is session-lifecycle-only (e.g. a socket-close
        /// signal) and is only ever meaningful at the moment it was originally emitted; replaying it later
        /// would act on stale intent rather than recover a legitimately dropped delivery.</summary>
        NotReplayable = 3,
    }
}
