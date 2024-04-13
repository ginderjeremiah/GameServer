using DataAccess.Entities.ItemMods;
using DataAccess.Entities.Items;
using DataAccess.Entities.PlayerAttributes;
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

        public BattlerAttribute(DataAccess.Entities.Enemies.AttributeDistribution distribution, int level)
        {
            AttributeId = (AttributeType)distribution.AttributeId;
            Amount = distribution.BaseAmount + distribution.AmountPerLevel * level;
        }

        public BattlerAttribute(ItemAttribute itemAttribute)
        {
            AttributeId = (AttributeType)itemAttribute.AttributeId;
            Amount = itemAttribute.Amount;
        }

        public BattlerAttribute(ItemModAttribute itemModAttribute)
        {
            AttributeId = (AttributeType)itemModAttribute.AttributeId;
            Amount = itemModAttribute.Amount;
        }

        public BattlerAttribute() { }
    }
}
