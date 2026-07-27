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

        // 2 and 3 are reserved for the equipment and mod streams (#2495), which are in flight on their own
        // branch. Two branches appending independently would otherwise both land on 2, and merging them would
        // give one persisted value two meanings — the one mistake this enum's append-only rule exists to stop.

        /// <summary>
        /// The player's equipped skill loadout (<c>SelectedSkillsChangedEvent</c>). Player-scoped — the event
        /// carries the full ordered loadout and the handler rebuilds every one of the player's
        /// <c>Selected</c>/<c>Order</c> columns from it, so the loadout <em>is</em> the target and a per-skill
        /// key would let one skill's row accept a write the same event's other rows rejected.
        /// </summary>
        SelectedSkills = 4,

        /// <summary>
        /// A player's log preferences (<c>LogPreferenceChangedEvent</c>), keyed per log type (<c>"{logTypeId}"</c>)
        /// — each event carries one type's flag, so a per-player key would discard an older change to type A
        /// merely because a newer change to type B landed first.
        /// </summary>
        LogPreference = 5,

        /// <summary>
        /// The player's stat-point allocations (<c>AttributeAllocationsChangedEvent</c>). Player-scoped: the
        /// event carries the complete per-attribute spread rather than a dirty subset, so a newer event
        /// supersedes an older one wholesale and there is nothing finer for a per-attribute key to save.
        /// </summary>
        AttributeAllocations = 6,

        /// <summary>
        /// The favorite flag on a player's unlocked items (<c>ItemFavoriteChangedEvent</c>), keyed per item
        /// (<c>"{itemId}"</c>). Deliberately its own stream rather than sharing the equipment stream's item key
        /// (#2495): the two write disjoint columns of the same row, so one key would make a favorite toggle
        /// reject an equip that is not stale in any sense that matters.
        /// </summary>
        ItemFavorite = 7,

        /// <summary>
        /// The read timestamp on a player's lessons (<c>LessonReadEvent</c>), keyed per lesson
        /// (<c>"{lessonId}"</c>).
        /// </summary>
        LessonRead = 8,
    }
}
