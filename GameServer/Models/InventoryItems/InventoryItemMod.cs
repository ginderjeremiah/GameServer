namespace GameServer.Models.InventoryItems
{
    public class InventoryItemMod
    {
        public int ItemModId { get; set; }
        public int ItemModSlotId { get; set; }

        public InventoryItemMod(GameCore.Entities.InventoryItemMod itemMod)
        {
            ItemModId = itemMod.ItemModId;
            ItemModSlotId = itemMod.ItemModSlotId;
        }
    }
}
