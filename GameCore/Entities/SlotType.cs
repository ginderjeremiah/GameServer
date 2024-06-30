using System.Text.Json.Serialization;

namespace GameCore.Entities
{
    public class SlotType
    {
        public int Id { get; set; }
        public string Name { get; set; }

        [JsonIgnore]
        public virtual List<ItemMod> ItemMods { get; set; }
        [JsonIgnore]
        public virtual List<ItemSlot> ItemSlots { get; set; }
    }
}
