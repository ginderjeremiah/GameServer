using System.Text.Json.Serialization;

namespace GameCore.Entities
{
    public class ItemModSlotType
    {
        public int Id { get; set; }
        public string Name { get; set; }

        [JsonIgnore]
        public virtual List<ItemMod> ItemMods { get; set; }
        [JsonIgnore]
        public virtual List<ItemModSlot> ItemModSlots { get; set; }
    }
}
