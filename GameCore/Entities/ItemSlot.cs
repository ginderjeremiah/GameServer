namespace GameCore.Entities
{
    public class ItemSlot
    {
        public int Id { get; set; }
        public int ItemId { get; set; }
        public int SlotTypeId { get; set; }
        public int GuaranteedItemModId { get; set; }
        public decimal Probability { get; set; }

        public virtual Item Item { get; set; }
        public virtual SlotType SlotType { get; set; }
        public virtual ItemMod GuaranteedItemMod { get; set; }
        public virtual List<InventoryItemMod> InventoryItemMods { get; set; }
    }
}
