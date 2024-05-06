using GameCore.Entities.ItemMods;
using GameCore.Entities.Items;
using GameCore.Entities.PlayerAttributes;

namespace GameCore.BattleSimulation
{
    public class BattlerAttribute
    {
        public AttributeType AttributeId { get; set; }
        public decimal Amount { get; set; }

        public BattlerAttribute(PlayerAttribute playerAttribute)
        {
            AttributeId = (AttributeType)playerAttribute.AttributeId;
            Amount = playerAttribute.Amount;
        }

        public BattlerAttribute(Entities.Enemies.AttributeDistribution distribution, int level)
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
