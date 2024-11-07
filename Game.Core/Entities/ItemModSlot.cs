using System.Text.Json.Serialization;

namespace Game.Core.Entities
{
    public partial class ItemModSlot
    {
        public int Id { get; set; }
        public int ItemId { get; set; }
        public int ItemModSlotTypeId { get; set; }
        public int? GuaranteedItemModId { get; set; }
        public decimal Probability { get; set; }

        [JsonIgnore]
        public virtual Item Item { get; set; }
        public virtual ItemModSlotType ItemModSlotType { get; set; }
        public virtual ItemMod? GuaranteedItemMod { get; set; }
        public virtual List<InventoryItemMod> InventoryItemMods { get; set; }
    }
}
