using GameCore;
using GameCore.Entities;

namespace GameServer.Models.Attributes
{
    public class AttributeMultiplier
    {
        public EAttribute AttributeId { get; set; }
        public decimal Multiplier { get; set; }

        public AttributeMultiplier(SkillDamageMultiplier attributeMultiplier)
        {
            AttributeId = (EAttribute)attributeMultiplier.AttributeId;
            Multiplier = attributeMultiplier.Multiplier;
        }
    }
}
