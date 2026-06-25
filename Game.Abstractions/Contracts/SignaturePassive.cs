using Game.Core;

namespace Game.Abstractions.Contracts
{
    /// <summary>Read contract for a class's signature passive — the durable combat-identity bonus the live
    /// frontend battler composes into its attributes so they match the backend snapshot
    /// (<c>BattleSnapshot</c>) the anti-cheat replay measures against (spike #1126 area E). Flat
    /// (<see cref="ScalingAttributeId"/> null) or attribute-scaled (<c>Amount + ScalingAttributeId ×
    /// ScalingAmount</c>). Delivered with the player (its class is fixed) alongside the locked-base
    /// fingerprint.</summary>
    public class SignaturePassive : IModel
    {
        /// <summary>The attribute the passive boosts.</summary>
        public EAttribute AttributeId { get; set; }

        /// <summary>The flat amount of the passive.</summary>
        public decimal Amount { get; set; }

        /// <summary>The attribute whose value scales the passive, or null when it is purely flat.</summary>
        public EAttribute? ScalingAttributeId { get; set; }

        /// <summary>The per-point amount applied to <see cref="ScalingAttributeId"/>'s value (0 when flat).</summary>
        public decimal ScalingAmount { get; set; }

        /// <summary>How the passive is applied (additive / multiplicative).</summary>
        public EModifierType ModifierType { get; set; }
    }
}
