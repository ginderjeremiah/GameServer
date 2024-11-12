using System.Text.Json.Serialization;

namespace Game.Core.Entities
{
    public partial class ItemModType
    {
        public int Id { get; set; }
        public string Name { get; set; }

        [JsonIgnore]
        public virtual List<ItemMod> ItemMods { get; set; }
        [JsonIgnore]
        public virtual List<ItemModSlot> ItemModSlots { get; set; }
    }
}
