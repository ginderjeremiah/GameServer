using Game.Core.Attributes.Modifiers;

namespace Game.Core.Attributes
{
    /// <summary>
    /// An exception that is thrown when adding an <see cref="AttributeModifier"/> that causes a circular dependency.
    /// </summary>
    public class AttributeCircularDerivedModifierException : Exception
    {
        /// <summary>
        /// The <see cref="AttributeModifier"/> that caused the exception.
        /// </summary>
        public AttributeModifier Modifier { get; set; }

        /// <summary>
        /// The default constructor for the exception.
        /// </summary>
        /// <param name="modifier"></param>
        public AttributeCircularDerivedModifierException(AttributeModifier modifier)
            : base($"A circular dependency was detected between {modifier.Attribute} and {modifier.DerivedSource}.")
        {
            Modifier = modifier;
        }
    }
}
