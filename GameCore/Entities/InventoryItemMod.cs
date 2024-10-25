using System.Text.Json.Serialization;

namespace GameCore.Entities
{
    public class InventoryItemMod
    {
        public int InventoryItemId { get; set; }
        public int ItemModId { get; set; }
        public int ItemModSlotId { get; set; }

        [JsonIgnore]
        public virtual InventoryItem InventoryItem { get; set; }
        public virtual ItemMod ItemMod { get; set; }
        public virtual ItemModSlot ItemModSlot { get; set; }
    }
}
