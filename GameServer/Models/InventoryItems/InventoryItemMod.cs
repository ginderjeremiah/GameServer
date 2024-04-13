namespace GameServer.Models.InventoryItems
{
    public class InventoryItemMod
    {
        public int ItemModId { get; set; }
        public int ItemSlotId { get; set; }

        public InventoryItemMod(DataAccess.Entities.InventoryItems.InventoryItemMod itemMod)
        {
            ItemModId = itemMod.ItemModId;
            ItemSlotId = itemMod.ItemSlotId;
        }
    }
}
