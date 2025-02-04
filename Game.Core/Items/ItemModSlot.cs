namespace Game.Core.Items
{
    /// <summary>
    /// Represents a slot in an item that can have a mod applied to it.
    /// </summary>
    public class ItemModSlot
    {
        /// <summary>
        /// The type of item mod that can be applied to this slot.
        /// </summary>
        public required EItemModType Type { get; set; }

        /// <summary>
        /// The probability that this slot will contain a mod.
        /// </summary>
        public required decimal Probability { get; set; }

        /// <summary>
        /// The index of this slot.
        /// </summary>
        public required int Index { get; set; }

        /// <summary>
        /// The item mod currently in this slot.
        /// </summary>
        public ItemMod? ItemMod { get; set; }

        /// <summary>
        /// The possible item mods that can be applied to this slot.
        /// </summary>
        public required List<ItemMod> PossibleItemMods { get; set; }

        ///// <summary>
        ///// Clones this item mod slot.
        ///// </summary>
        ///// <returns></returns>
        //public ItemModSlot Clone()
        //{
        //    return new ItemModSlot
        //    {
        //        Type = Type,
        //        Index = Index,
        //        Probability = Probability,
        //        ItemMod = ItemMod,
        //        PossibleItemMods = PossibleItemMods
        //    };
        //}
    }
}
