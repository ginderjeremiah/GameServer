using GameCore;

namespace GameServer.Models.Attributes
{
    public class AttributeDistribution : IModel
    {
        public EAttribute AttributeId { get; set; }
        public decimal BaseAmount { get; set; }
        public decimal AmountPerLevel { get; set; }

        public AttributeDistribution(GameCore.Entities.AttributeDistribution dist)
        {
            AttributeId = (EAttribute)dist.AttributeId;
            BaseAmount = dist.BaseAmount;
            AmountPerLevel = dist.AmountPerLevel;
        }
    }
}
