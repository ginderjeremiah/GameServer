using Game.Core.Attributes.Modifiers;

namespace Game.Core.Proficiencies
{
    /// <summary>
    /// An attribute bonus a proficiency grants on reaching a level. Converted to a battle
    /// <see cref="AttributeModifier"/> (with a <see cref="EAttributeModifierSource.Proficiency"/> source) when
    /// the player's battler is assembled from the battle snapshot.
    /// </summary>
    public class ProficiencyModifier
    {
        public required EAttribute Attribute { get; init; }
        public required EModifierType ModifierType { get; init; }
        public required double Amount { get; init; }

        /// <summary>
        /// Converts this proficiency bonus into the battle <see cref="AttributeModifier"/> it contributes to a
        /// player's battle attributes (source <see cref="EAttributeModifierSource.Proficiency"/>). Single
        /// source of truth for the proficiency-bonus → modifier rule, consumed at battler assembly via
        /// <see cref="Proficiency.ModifiersForLevel"/>.
        /// </summary>
        public AttributeModifier ToAttributeModifier() => new()
        {
            Attribute = Attribute,
            Amount = Amount,
            Type = ModifierType,
            Source = EAttributeModifierSource.Proficiency,
        };
    }
}
