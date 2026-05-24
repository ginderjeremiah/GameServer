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

        public InventoryItem(Abstractions.Entities.InventoryItem invItem)
        {
            Id = invItem.Id;
            ItemId = invItem.ItemId;
            Rating = invItem.Rating;
            Equipped = invItem.Equipped;
            InventorySlotNumber = invItem.InventorySlotNumber;
            ItemMods = invItem.InventoryItemMods.Select(mod => new InventoryItemMod(mod)).ToList();
        }

        /// <summary>
        /// Constructs an <see cref="InventoryItem"/> model for a freshly dropped (unequipped)
        /// item using domain data — used when the entity record exists but we only have domain
        /// objects at hand.
        /// </summary>
        public InventoryItem(int inventoryItemId, int slotNumber, Core.Items.Item coreItem)
        {
            Id = inventoryItemId;
            ItemId = coreItem.Id;
            Rating = 1;
            Equipped = false;
            InventorySlotNumber = slotNumber;
            ItemMods = [];
        }
    }
}
