using GameCore;
using GameCore.Entities;

namespace GameServer.Models.Attributes
{
    public class BattlerAttribute : IModel
    {
        public EAttribute AttributeId { get; set; }
        public decimal Amount { get; set; }

        public BattlerAttribute() { }

        public BattlerAttribute(GameCore.BattleSimulation.BattlerAttribute battlerAttribute)
        {
            AttributeId = battlerAttribute.AttributeId;
            Amount = battlerAttribute.Amount;
        }

        public BattlerAttribute(ItemModAttribute itemModAttribute)
        {
            AttributeId = (EAttribute)itemModAttribute.AttributeId;
            Amount = itemModAttribute.Amount;
        }

        public BattlerAttribute(ItemAttribute itemAttribute)
        {
            AttributeId = (EAttribute)itemAttribute.AttributeId;
            Amount = itemAttribute.Amount;
        }
        public BattlerAttribute(PlayerAttribute playerAttribute)
        {
            AttributeId = (EAttribute)playerAttribute.AttributeId;
            Amount = playerAttribute.Amount;
        }
    }
}
