using System.Text.Json.Serialization;

namespace Game.Core.Entities
{
    public partial class ItemAttribute
    {
        public int ItemId { get; set; }
        public int AttributeId { get; set; }
        public decimal Amount { get; set; }

        [JsonIgnore]
        public virtual Item Item { get; set; }
        public virtual Attribute Attribute { get; set; }
    }
}
