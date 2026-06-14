namespace Game.Core.Items
{
    /// <summary>
    /// Represents a slot in an item that can have a mod applied to it. Part of the shared, cached
    /// <see cref="Item"/> graph and therefore structurally immutable (init-only) so a consumer cannot
    /// set <see cref="ItemMod"/> on the shared instance and corrupt the cache for every player (#547).
    /// </summary>
    public class ItemModSlot
    {
        /// <summary>
        /// The database ID of this slot.
        /// </summary>
        public required int Id { get; init; }

        /// <summary>
        /// The type of item mod that can be applied to this slot.
        /// </summary>
        public required EItemModType Type { get; init; }

        /// <summary>
        /// The item mod currently applied in this slot (if any).
        /// </summary>
        public ItemMod? ItemMod { get; init; }
    }
}
