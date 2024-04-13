namespace GameServer.Models.Attributes
{
    public class AttributeMultiplier
    {
        public AttributeType AttributeId { get; set; }
        public decimal Multiplier { get; set; }

        public AttributeMultiplier(DataAccess.Entities.Skills.SkillDamageMultiplier attributeMultiplier)
        {
            AttributeId = (AttributeType)attributeMultiplier.AttributeId;
            Multiplier = attributeMultiplier.Multiplier;
        }
    }
}
