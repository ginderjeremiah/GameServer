using static Game.Core.EModifierType;

namespace Game.Core.Attributes.Modifiers
{
    /// <summary>
    /// A class used to represent a modifier for a particular <see cref="EAttribute"/>.
    /// </summary>
    public class AttributeModifier
    {
        /// <summary>
        /// The enum value of the <see cref="Attributes.Attribute"/> the modifier is for.
        /// </summary>
        public required EAttribute Attribute { get; set; }

        /// <summary>
        /// The amount of the modifier.
        /// </summary>
        public required double Amount { get; set; }

        /// <inheritdoc cref="EModifierType"/>
        public required EModifierType Type { get; set; }

        /// <inheritdoc cref="EAttributeModifierSource"/>
        public required EAttributeModifierSource Source { get; set; }

        /// <summary>
        /// The <see cref="EAttribute"/> this modifier is derived from if the <see cref="Source"/> is <see cref="EAttributeModifierSource.Derived"/>.
        /// </summary>
        public EAttribute DerivedSource { get; set; }

        /// <summary>
        /// Applies this modifier to the given <paramref name="currentValue"/>.
        /// </summary>
        /// <param name="currentValue"></param>
        /// <param name="store"></param>
        /// <returns></returns>
        public virtual double Apply(double currentValue, AttributeCollection store)
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
                _ => currentValue,
            };
        }
    }
}
