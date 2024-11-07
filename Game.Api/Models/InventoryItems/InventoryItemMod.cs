namespace Game.Api.Models.InventoryItems
{
    public class InventoryItemMod
    {
        public int ItemModId { get; set; }
        public int ItemModSlotId { get; set; }

        public InventoryItemMod(Core.Entities.InventoryItemMod itemMod)
        {
            ItemModId = itemMod.ItemModId;
            ItemModSlotId = itemMod.ItemModSlotId;
        }
    }
}
