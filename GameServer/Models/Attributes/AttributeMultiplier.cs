namespace GameServer.Models.Attributes
{
    public class AttributeMultiplier
    {
        public AttributeType AttributeId { get; set; }
        public decimal Multiplier { get; set; }

        public AttributeMultiplier(DataAccess.Models.Attributes.AttributeMultiplier attributeMultiplier)
        {
            AttributeId = (AttributeType)attributeMultiplier.AttributeId;
            Multiplier = attributeMultiplier.Multiplier;
        }
    }
}
