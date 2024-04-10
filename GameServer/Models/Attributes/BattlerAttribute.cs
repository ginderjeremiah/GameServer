using DataAccess.Models.Items;
using DataAccess.Models.PlayerAttributes;
namespace GameServer.Models.Attributes
{
    public class BattlerAttribute : IModel
    {
        public AttributeType AttributeId { get; set; }
        public decimal Amount { get; set; }

        public BattlerAttribute(PlayerAttribute playerAttribute)
        {
            AttributeId = (AttributeType)playerAttribute.AttributeId;
            Amount = playerAttribute.Amount;
        }

        public BattlerAttribute(DataAccess.Models.Attributes.AttributeDistribution distribution, int level)
        {
            AttributeId = (AttributeType)distribution.AttributeId;
            Amount = distribution.BaseAmount + distribution.AmountPerLevel * level;
        }

        public BattlerAttribute(ItemAttribute itemAttribute)
        {
            AttributeId = (AttributeType)itemAttribute.AttributeId;
            Amount = itemAttribute.Amount;
        }

        public BattlerAttribute() { }
    }
}
