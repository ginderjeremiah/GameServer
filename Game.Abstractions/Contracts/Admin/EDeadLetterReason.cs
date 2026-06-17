namespace Game.Abstractions.Contracts.Admin
{
    /// <summary>
    /// Why a player write-behind event is sitting on the dead-letter queue. Surfaced by the inspection
    /// surface so an operator can tell a genuinely-poison entry (pointless to replay) from one worth
    /// replaying once its transient cause has cleared.
    /// </summary>
    public enum EDeadLetterReason
    {
        /// <summary>The stored message could not be parsed into an event envelope (malformed or empty);
        /// replaying it will just re-fail and re-dead-letter.</summary>
        Malformed = 0,

        /// <summary>The envelope parsed but names an event type the synchronizer does not handle;
        /// replaying it will just re-fail and re-dead-letter.</summary>
        UnknownEventType = 1,

        /// <summary>A well-formed, known event that exhausted its retries on a transient failure; worth
        /// replaying once the underlying cause is fixed.</summary>
        Replayable = 2,
    }
}
