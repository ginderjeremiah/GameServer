namespace Game.Core.Items
{
    /// <summary>
    /// Represents a slot in an item that can have a mod applied to it.
    /// </summary>
    public class ItemModSlot
    {
        /// <summary>
        /// The database ID of this slot.
        /// </summary>
        public required int Id { get; set; }

        /// <summary>
        /// The type of item mod that can be applied to this slot.
        /// </summary>
        public required EItemModType Type { get; set; }

        /// <summary>
        /// The item mod currently applied in this slot (if any).
        /// </summary>
        public ItemMod? ItemMod { get; set; }
    }
}
