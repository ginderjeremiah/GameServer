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
        /// <summary>
        /// The value <see cref="Sequence"/> carries when the envelope holds no ordering information at all —
        /// an envelope enqueued by a pre-upgrade instance mid-rolling-deploy, which deserializes with no
        /// "sequence" property. It is a <em>sentinel, not a low sequence</em>: the consuming guard (#2474)
        /// applies such an envelope unconditionally and leaves the target's watermark untouched, which is
        /// exactly the pre-guard behaviour. Comparing it as a real sequence instead would make it lose every
        /// comparison, silently discarding every write of a session that lands on a pre-upgrade instance after
        /// an upgraded one already advanced that player's watermarks. Real counters therefore start at 1.
        /// </summary>
        public const long Unsequenced = 0;

        public Guid Id { get; init; } = Guid.NewGuid();
        public required string Type { get; set; }
        public required string Payload { get; set; }

        /// <summary>
        /// The producing aggregate's write sequence at the time this envelope was <em>buffered</em> (#2473).
        /// Stamped at buffer time rather than flush time so envelopes carried forward from a failed flush into
        /// a later save's flush (#1494) keep their original, lower value and are correctly recognised as the
        /// older write. Defaulted rather than required, so a pre-upgrade instance's envelope still deserializes
        /// — see <see cref="Unsequenced"/> for why that default must never be read as "oldest".
        /// </summary>
        public long Sequence { get; init; } = Unsequenced;
    }
}
