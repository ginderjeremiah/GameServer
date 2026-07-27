namespace Game.Infrastructure.Entities
{
    /// <summary>
    /// Identifies which write-behind target space a <see cref="PlayerWriteWatermark"/> row guards. One value
    /// per space rather than per handler: handlers writing disjoint tables must never contend on the same
    /// watermark row, but handlers writing the <em>same</em> rows must share a stream or they cannot order
    /// against each other — a stale <c>ModApplied</c> is only rejected because <c>ModRemoved</c> advanced the
    /// very watermark it compares against. The meaning of a stream's
    /// <see cref="PlayerWriteWatermark.TargetKey"/> is fixed by the stream (see the format on each member).
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

        /// <summary>
        /// The player's equipped-item slots (<c>ItemEquippedEvent</c>, <c>ItemUnequippedEvent</c>). Keyed on
        /// both the item and the destination slot (<c>"item:{itemId}"</c>, <c>"slot:{slotId}"</c>) because an
        /// equip writes two identities at once and either key alone leaves a hole: the slot key stops a
        /// replayed older equip into a slot a newer one now owns, and the item key stops a replayed
        /// <c>A→slot1</c> after A moved on to slot2 (which leaves slot1 untouched, so only the item key can
        /// see it). Unequip involves no slot and carries the item key alone.
        /// </summary>
        Equipment = 2,

        /// <summary>
        /// The mods applied to a player's item mod slots (<c>ModAppliedEvent</c>, <c>ModRemovedEvent</c>),
        /// keyed per mod slot (<c>"{itemId}:{modSlotId}"</c>) — the identity of the row both handlers write.
        /// This is the stream the separate-row design exists for: <c>ModRemovedHandler</c> deletes its row
        /// outright, so a per-row version column would go to the grave with it and a stale <c>ModApplied</c>
        /// arriving afterwards would find nothing to compare against and resurrect the mod.
        /// </summary>
        Mods = 3,
    }
}
