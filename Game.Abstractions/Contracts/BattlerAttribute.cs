using Game.Core;

namespace Game.Abstractions.Contracts
{
    /// <summary>Read contract for a flat attribute amount carried by items, item mods and players.</summary>
    public class BattlerAttribute : IModel
    {
        public EAttribute AttributeId { get; set; }
        public decimal Amount { get; set; }

        /// <summary>
        /// Projects an attribute/amount pair onto the wire contract — the single source of truth for the
        /// <c>(decimal)</c> cast applied to every domain attribute amount, shared by all sites that emit a
        /// <see cref="BattlerAttribute"/> (enemy modifiers and player stat allocations).
        /// </summary>
        public static BattlerAttribute From(EAttribute attribute, double amount)
        {
            return new BattlerAttribute
            {
                AttributeId = attribute,
                Amount = (decimal)amount,
            };
        }
    }
}
