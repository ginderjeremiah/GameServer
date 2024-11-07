using Game.Core;
using Game.Core.Entities;
using CoreBattlerAttribute = Game.Core.BattleSimulation.BattlerAttribute;

namespace Game.Api.Models.Attributes
{
    public class BattlerAttribute
        : IModelFromSource<BattlerAttribute, CoreBattlerAttribute>,
          IModelFromSource<BattlerAttribute, ItemAttribute>,
          IModelFromSource<BattlerAttribute, ItemModAttribute>,
          IModelFromSource<BattlerAttribute, PlayerAttribute>
    {
        public EAttribute AttributeId { get; set; }
        public decimal Amount { get; set; }

        public static BattlerAttribute FromSource(CoreBattlerAttribute battlerAttribute)
        {
            return new BattlerAttribute
            {
                AttributeId = battlerAttribute.AttributeId,
                Amount = battlerAttribute.Amount,
            };
        }
        public static BattlerAttribute FromSource(ItemAttribute itemAttribute)
        {
            return new BattlerAttribute
            {
                AttributeId = (EAttribute)itemAttribute.AttributeId,
                Amount = itemAttribute.Amount,
            };
        }

        public static BattlerAttribute FromSource(ItemModAttribute itemModAttribute)
        {
            return new BattlerAttribute
            {
                AttributeId = (EAttribute)itemModAttribute.AttributeId,
                Amount = itemModAttribute.Amount,
            };
        }

        public static BattlerAttribute FromSource(PlayerAttribute playerAttribute)
        {
            return new BattlerAttribute
            {
                AttributeId = (EAttribute)playerAttribute.AttributeId,
                Amount = playerAttribute.Amount,
            };
        }
    }
}
