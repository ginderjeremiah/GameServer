using DataAccess.Models.PlayerAttributes;
using static GameServer.AttributeType;
namespace GameServer.Models.Attributes
{
    public class BattlerAttribute : IModel
    {
        public AttributeType AttributeId { get; set; }
        public decimal Amount { get; set; }
        public bool IsCoreAttribute => AttributeId is Strength or Endurance or Intellect or Agility or Dexterity or Luck;

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

        public BattlerAttribute() { }
    }
}
