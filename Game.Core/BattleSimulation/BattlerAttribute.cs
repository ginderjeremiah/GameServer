using Game.Core.Entities;

namespace Game.Core.BattleSimulation
{
    /// <summary>
    /// A class used 
    /// </summary>
    public class BattlerAttribute
    {
        public EAttribute AttributeId { get; set; }
        public decimal Amount { get; set; }

        public BattlerAttribute(PlayerAttribute playerAttribute)
        {
            AttributeId = (EAttribute)playerAttribute.AttributeId;
            Amount = playerAttribute.Amount;
        }

        public BattlerAttribute(AttributeDistribution distribution, int level)
        {
            AttributeId = (EAttribute)distribution.AttributeId;
            Amount = distribution.BaseAmount + (distribution.AmountPerLevel * level);
        }

        public BattlerAttribute(ItemAttribute itemAttribute)
        {
            AttributeId = (EAttribute)itemAttribute.AttributeId;
            Amount = itemAttribute.Amount;
        }

        public BattlerAttribute(ItemModAttribute itemModAttribute)
        {
            AttributeId = (EAttribute)itemModAttribute.AttributeId;
            Amount = itemModAttribute.Amount;
        }

        public BattlerAttribute() { }
    }
}
