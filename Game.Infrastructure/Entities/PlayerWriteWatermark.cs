namespace Game.Infrastructure.Entities
{
    /// <summary>
    /// The highest write sequence already applied to one write-behind target, so the drain can reject a write
    /// older than what the target already holds (#2474). Without it, a stale absolute write replayed after a
    /// newer one — a parked event reclaimed after another instance already applied the player's next save —
    /// durably regresses the row, and Redis (the source of truth) never self-corrects it until the player
    /// dirties the same row again.
    /// </summary>
    /// <remarks>
    /// The row is the per-target serialization point, not merely a record: the guard's conditional update takes
    /// its lock <em>before</em> the data write, in the same transaction, so two instances applying the same
    /// player's events concurrently can't both pass a read-then-compare and let the older one commit last.
    /// It is also deliberately independent of the data row it guards — <c>ModRemovedHandler</c> deletes its row
    /// outright, and a per-row version column would go to the grave with it.
    /// </remarks>
    public class PlayerWriteWatermark
    {
        public int PlayerId { get; set; }
        public PlayerWriteStream Stream { get; set; }

        /// <summary>
        /// The canonical identity of the guarded target within <see cref="Stream"/>, empty for a genuinely
        /// player-scoped stream. Format is owned by the stream — see <see cref="PlayerWriteStream"/>.
        /// </summary>
        public required string TargetKey { get; set; }

        /// <summary>
        /// The highest sequence applied to this target. Never <c>0</c> — that is the "unsequenced" sentinel an
        /// envelope from a pre-upgrade instance carries, and such an envelope bypasses the guard entirely
        /// rather than seeding or advancing a watermark.
        /// </summary>
        public long LastAppliedSequence { get; set; }
    }
}
