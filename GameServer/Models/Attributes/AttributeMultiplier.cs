using GameCore.BattleSimulation;
using GameCore.Entities;

namespace GameServer.Models.Attributes
{
    public class AttributeMultiplier
    {
        public AttributeType AttributeId { get; set; }
        public decimal Multiplier { get; set; }

        public AttributeMultiplier(SkillDamageMultiplier attributeMultiplier)
        {
            AttributeId = (AttributeType)attributeMultiplier.AttributeId;
            Multiplier = attributeMultiplier.Multiplier;
        }
    }
}
