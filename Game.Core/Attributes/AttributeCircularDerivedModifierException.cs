using Game.Core.Attributes.Modifiers;

namespace Game.Core.Attributes
{
    /// <summary>
    /// Thrown when adding a derived <see cref="AttributeModifier"/> would create a <b>direct</b>
    /// (length-2) cycle between two attributes — the target already derives from the modifier's
    /// source while the new modifier makes that source derive back from the target.
    /// </summary>
    /// <remarks>
    /// This guard intentionally detects only direct 2-cycles, not longer derived chains
    /// (e.g. A→B→C→A). That is sufficient because the only derived modifiers today come from the
    /// static, acyclic <see cref="StaticAttributeModifiers"/>, so a longer cycle is unreachable in
    /// practice. If derived modifiers ever become authorable (e.g. gear- or skill-effect-sourced),
    /// this must be upgraded to a full cycle check — an undetected longer cycle would otherwise
    /// recurse unbounded during attribute evaluation and end in a stack overflow.
    /// </remarks>
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
