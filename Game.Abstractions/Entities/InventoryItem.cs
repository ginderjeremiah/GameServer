namespace Game.Abstractions.Entities
{
    public partial class InventoryItem
    {
        public int Id { get; set; }
        public int PlayerId { get; set; }
        public int ItemId { get; set; }
        public int Rating { get; set; }
        public bool Equipped { get; set; }
        public int InventorySlotNumber { get; set; }

        public virtual List<InventoryItemMod> InventoryItemMods { get; set; }
        public virtual Player Player { get; set; }
        public virtual Item Item { get; set; }
    }
}
