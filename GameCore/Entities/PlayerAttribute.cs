using System.Text.Json.Serialization;

namespace GameCore.Entities
{
    public class PlayerAttribute
    {
        public int PlayerId { get; set; }
        public int AttributeId { get; set; }
        public decimal Amount { get; set; }

        [JsonIgnore]
        public virtual Player Player { get; set; }
        public virtual Attribute Attribute { get; set; }
    }
}
