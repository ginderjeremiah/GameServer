namespace Game.DataAccess
{
    /// <summary>
    /// A queued player-update event. <see cref="Id"/> gives every envelope a unique identity even when two
    /// independently-raised events serialize to byte-identical Type+Payload (e.g. two duplicate
    /// <c>SkillUnlockedEvent</c>s, or two <c>ItemFavoriteChangedEvent</c>s toggling to the same value) — the
    /// write-behind queue's stranded-head tracking and LREM acknowledge/dead-letter removal all key off the raw
    /// serialized string, and equal payloads would otherwise alias one another there (#2341). Defaulted rather
    /// than required so an envelope enqueued by a pre-upgrade instance mid-rolling-deploy (carrying no "id")
    /// still deserializes cleanly on a newer instance.
    /// </summary>
    internal class DomainEventEnvelope
    {
        public Guid Id { get; init; } = Guid.NewGuid();
        public required string Type { get; set; }
        public required string Payload { get; set; }
    }
}
