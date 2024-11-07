using Game.Core;
using Game.Core.Entities;

namespace Game.Api.Models.Attributes
{
    public class AttributeMultiplier : IModelFromSource<AttributeMultiplier, SkillDamageMultiplier>
    {
        public EAttribute AttributeId { get; set; }
        public decimal Multiplier { get; set; }

        public static AttributeMultiplier FromSource(SkillDamageMultiplier attributeMultiplier)
        {
            return new AttributeMultiplier
            {
                AttributeId = (EAttribute)attributeMultiplier.AttributeId,
                Multiplier = attributeMultiplier.Multiplier,
            };
        }
    }
}
