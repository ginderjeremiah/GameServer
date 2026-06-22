namespace Game.Core.Proficiencies
{
    /// <summary>
    /// An attribute bonus a proficiency grants on reaching a level. Converted to a battle
    /// <see cref="Attributes.Modifiers.AttributeModifier"/> (with a Proficiency source) when the player's
    /// battler is assembled — that wiring lands in a later sub-issue.
    /// </summary>
    public class ProficiencyModifier
    {
        public required EAttribute Attribute { get; init; }
        public required EModifierType ModifierType { get; init; }
        public required double Amount { get; init; }
    }
}
