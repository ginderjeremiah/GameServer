namespace GameServer.Models.InventoryItems
{
    public class InventoryItem : IModel
    {
        public int InventoryItemId { get; set; }
        public int ItemId { get; set; }
        public int Rating { get; set; }
        public bool Equipped { get; set; }
        public int InventorySlotNumber { get; set; }
        public List<InventoryItemMod> ItemMods { get; set; }

        public InventoryItem(DataAccess.Entities.InventoryItems.InventoryItem invItem)
        {
            InventoryItemId = invItem.InventoryItemId;
            ItemId = invItem.ItemId;
            Rating = invItem.Rating;
            Equipped = invItem.Equipped;
            InventorySlotNumber = invItem.InventorySlotNumber;
            ItemMods = invItem.ItemMods.Select(mod => new InventoryItemMod(mod)).ToList();
        }
    }
}
