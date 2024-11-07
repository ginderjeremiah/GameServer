using System.Text.Json.Serialization;

namespace Game.Core.Entities
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
        [JsonIgnore]
        public virtual Player Player { get; set; }
        public virtual Item Item { get; set; }
    }
}
