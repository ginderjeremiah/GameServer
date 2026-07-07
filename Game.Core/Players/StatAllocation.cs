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
        /// player's battle attributes. Single source of truth for the allocation → modifier rule, used by the
        /// battle-snapshot reconstruction (<see cref="Battle.BattleSnapshot.ToBattler"/>) and the
        /// <c>Game.Core.TestInfrastructure</c> attribute-composition shortcut.
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

        /// <summary>
        /// Creates an independent copy of this allocation. Used to capture an immutable point-in-time
        /// copy for the battle snapshot (<see cref="Battle.BattleSnapshot.FromPlayer"/>) so that later
        /// in-place mutations of the live allocation (e.g. <see cref="PlayerStatPoints.TryUpdateAttributes"/>
        /// adjusting <see cref="Amount"/>) cannot retroactively alter the snapshot. Centralizing the copy
        /// here keeps it correct if the allocation ever gains additional fields.
        /// </summary>
        public StatAllocation Copy()
        {
            return new StatAllocation
            {
                Attribute = Attribute,
                Amount = Amount,
            };
        }
    }
}
