using static Game.Core.EModifierType;

namespace Game.Core.Attributes.Modifiers
{
    /// <summary>
    /// An immutable value object representing a modifier for a particular <see cref="EAttribute"/>. Every
    /// property is <c>init</c>-only so a shared instance — inside the cached read-only reference-data
    /// collections (#547) or as a battle-engine primitive — can never be mutated and corrupt the graph for
    /// every player (#603). The battle add/remove path swaps whole instances and relies on reference
    /// identity to remove a specific one, both of which immutability preserves.
    /// </summary>
    public sealed class AttributeModifier
    {
        /// <summary>
        /// The enum value of the <see cref="Attributes.Attribute"/> the modifier is for.
        /// </summary>
        public required EAttribute Attribute { get; init; }

        /// <summary>
        /// The amount of the modifier.
        /// </summary>
        public required double Amount { get; init; }

        /// <inheritdoc cref="EModifierType"/>
        public required EModifierType Type { get; init; }

        /// <inheritdoc cref="EAttributeModifierSource"/>
        public required EAttributeModifierSource Source { get; init; }

        /// <summary>
        /// The <see cref="EAttribute"/> this modifier is derived from if the <see cref="Source"/> is <see cref="EAttributeModifierSource.Derived"/>.
        /// </summary>
        public EAttribute DerivedSource { get; init; }

        /// <summary>
        /// Applies this modifier to the given <paramref name="currentValue"/>.
        /// </summary>
        /// <param name="currentValue"></param>
        /// <param name="store"></param>
        /// <returns></returns>
        public double Apply(double currentValue, AttributeCollection store)
        {
            var modifierAmount = Amount;
            if (Source is EAttributeModifierSource.Derived)
            {
                modifierAmount *= store.GetAttributeValue(DerivedSource);
            }

            return Type switch
            {
                Additive => currentValue + modifierAmount,
                Multiplicative => currentValue * modifierAmount,
                _ => throw new ModifierTypeNotSupportedException(Type),
            };
        }
    }
}
