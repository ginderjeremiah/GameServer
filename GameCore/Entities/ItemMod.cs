using System.Text.Json.Serialization;

namespace GameCore.Entities
{
    public class ItemMod
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public bool Removable { get; set; }
        public string Description { get; set; }
        public int SlotTypeId { get; set; }

        public virtual List<ItemModAttribute> ItemModAttributes { get; set; }
        public virtual ItemModSlotType SlotType { get; set; }
        [JsonIgnore]
        public virtual List<InventoryItemMod> InventoryItemMods { get; set; }
        public virtual List<ItemModSlot> GuaranteedSlots { get; set; }
        public virtual List<Tag> Tags { get; set; }
    }
}
