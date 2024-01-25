namespace DataAccess.Models.InventoryItems
{
    public class InventoryItem
    {
        public int InventoryItemId { get; set; }
        public int PlayerId { get; set; }
        public int ItemId { get; set; }
        public int Rating { get; set; }
        public bool Equipped { get; set; }
        public int SlotId { get; set; }
        public List<InventoryItemMod> ItemMods { get; set; }
    }
}
