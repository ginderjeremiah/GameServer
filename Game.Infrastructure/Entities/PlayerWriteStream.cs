namespace Game.Infrastructure.Entities
{
    /// <summary>
    /// Identifies which write-behind handler a <see cref="PlayerWriteWatermark"/> row guards. One value per
    /// guarded handler, so two handlers writing disjoint tables never contend on the same watermark row, and
    /// the meaning of a stream's <see cref="PlayerWriteWatermark.TargetKey"/> is fixed by the handler that
    /// owns it (see the per-stream key format on each member).
    /// </summary>
    /// <remarks>
    /// Persisted as its numeric value, so the enum grows append-only and a member is never renumbered.
    /// </remarks>
    public enum PlayerWriteStream
    {
        /// <summary>
        /// The player's own row (<c>PlayerCoreUpdatedEvent</c>). Genuinely player-scoped — one target — so its
        /// key is the empty string.
        /// </summary>
        PlayerCore = 0,

        /// <summary>
        /// The player's progress rows (<c>ProgressUpdatedEvent</c>): statistics, challenges, and proficiencies.
        /// Keyed per row (<c>"stat:{typeId}:{entityId}"</c>, <c>"challenge:{id}"</c>, <c>"prof:{id}"</c>)
        /// because a progress event carries only a save's <em>dirty</em> rows — a coarser key would let a newer
        /// event covering one row reject an older event covering a different, still-current one.
        /// </summary>
        Progress = 1,
    }
}
