using Game.Core.Attributes.Modifiers;

namespace Game.Core.Players
{
    /// <summary>
    /// Represents a stat allocation for a particular <see cref="EAttribute"/>.
    /// </summary>
    public class StatAllocation
    {
        public required EAttribute Attribute { get; set; }

        public required double Amount { get; set; }

        /// <summary>
        /// Converts this allocation into the additive <see cref="AttributeModifier"/> it contributes to a
        /// player's battle attributes. Single source of truth for the allocation → modifier rule, shared by
        /// the live <see cref="PlayerStatPoints.ToAttributeModifiers"/> path and the battle-snapshot
        /// reconstruction (<see cref="Battle.BattleSnapshot.ToBattler"/>).
        /// </summary>
        public AttributeModifier ToModifier()
        {
            return new AttributeModifier
            {
                Attribute = Attribute,
                Amount = Amount,
                Type = EModifierType.Additive,
                Source = EAttributeModifierSource.PlayerStatPoints,
            };
        }
    }
}
