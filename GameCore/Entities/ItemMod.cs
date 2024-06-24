namespace GameCore.Entities
{
    public class ItemMod
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public bool Removable { get; set; }
        public string Description { get; set; }
        public int SlotTypeId { get; set; }

        public virtual List<ItemModAttribute> ItemModAttributes { get; set; }
        public virtual SlotType SlotType { get; set; }
        public virtual List<InventoryItemMod> InventoryItemMods { get; set; }
        public virtual List<ItemSlot> GuaranteedSlots { get; set; }
        public virtual List<Tag> Tags { get; set; }
    }
}
