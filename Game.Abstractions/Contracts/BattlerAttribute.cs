using Game.Core;

namespace Game.Abstractions.Contracts
{
    /// <summary>Read contract for a flat attribute amount carried by items, item mods and players.</summary>
    public class BattlerAttribute : IModel
    {
        public EAttribute AttributeId { get; set; }
        public decimal Amount { get; set; }
    }
}
