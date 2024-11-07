using System.Text.Json.Serialization;

namespace Game.Core.Entities
{
    public partial class ItemModAttribute
    {
        public int ItemModId { get; set; }
        public int AttributeId { get; set; }
        public decimal Amount { get; set; }

        [JsonIgnore]
        public virtual ItemMod ItemMod { get; set; }
        public virtual Attribute Attribute { get; set; }
    }
}
