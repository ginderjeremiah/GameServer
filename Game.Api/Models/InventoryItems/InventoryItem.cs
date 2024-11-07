using Game.Api.Models;

namespace Game.Api.Models.InventoryItems
{
    public class InventoryItem : IModel
    {
        public int Id { get; set; }
        public int ItemId { get; set; }
        public int Rating { get; set; }
        public bool Equipped { get; set; }
        public int InventorySlotNumber { get; set; }
        public List<InventoryItemMod> ItemMods { get; set; }

        public InventoryItem(Core.Entities.InventoryItem invItem)
        {
            Id = invItem.Id;
            ItemId = invItem.ItemId;
            Rating = invItem.Rating;
            Equipped = invItem.Equipped;
            InventorySlotNumber = invItem.InventorySlotNumber;
            ItemMods = invItem.InventoryItemMods.Select(mod => new InventoryItemMod(mod)).ToList();
        }
    }
}
