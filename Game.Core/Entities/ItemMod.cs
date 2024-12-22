using System.Text.Json.Serialization;

namespace Game.Core.Entities
{
    public partial class ItemMod : IZeroBasedIdentityEntity
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public bool Removable { get; set; }
        public string Description { get; set; }
        public int ItemModTypeId { get; set; }

        public virtual List<ItemModAttribute> ItemModAttributes { get; set; }
        public virtual ItemModType ItemModType { get; set; }
        [JsonIgnore]
        public virtual List<InventoryItemMod> InventoryItemMods { get; set; }
        public virtual List<ItemModSlot> GuaranteedSlots { get; set; }
        public virtual List<Tag> Tags { get; set; }
    }
}
