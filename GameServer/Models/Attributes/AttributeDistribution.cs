namespace GameServer.Models.Attributes
{
    public class AttributeDistribution : IModel
    {
        public AttributeType AttributeId { get; set; }
        public decimal BaseAmount { get; set; }
        public decimal AmountPerLevel { get; set; }

        public AttributeDistribution(DataAccess.Models.Attributes.AttributeDistribution dist)
        {
            AttributeId = (AttributeType)dist.AttributeId;
            BaseAmount = dist.BaseAmount;
            AmountPerLevel = dist.AmountPerLevel;
        }
    }
}
