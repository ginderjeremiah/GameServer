using GameCore.BattleSimulation;
using GameCore.Entities;

namespace GameServer.Models.Attributes
{
    public class BattlerAttribute : IModel
    {
        public AttributeType AttributeId { get; set; }
        public decimal Amount { get; set; }

        public BattlerAttribute() { }

        public BattlerAttribute(GameCore.BattleSimulation.BattlerAttribute battlerAttribute)
        {
            AttributeId = battlerAttribute.AttributeId;
            Amount = battlerAttribute.Amount;
        }

        public BattlerAttribute(ItemModAttribute itemModAttribute)
        {
            AttributeId = (AttributeType)itemModAttribute.AttributeId;
            Amount = itemModAttribute.Amount;
        }

        public BattlerAttribute(ItemAttribute itemAttribute)
        {
            AttributeId = (AttributeType)itemAttribute.AttributeId;
            Amount = itemAttribute.Amount;
        }
        public BattlerAttribute(PlayerAttribute playerAttribute)
        {
            AttributeId = (AttributeType)playerAttribute.AttributeId;
            Amount = playerAttribute.Amount;
        }
    }
}
