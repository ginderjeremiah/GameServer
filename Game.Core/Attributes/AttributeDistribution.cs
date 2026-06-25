using Game.Core.Attributes.Modifiers;

namespace Game.Core.Attributes
{
    /// <summary>
    /// Represents the level-scaled distribution of an attribute (<c>BaseAmount + AmountPerLevel × level</c>).
    /// Reused as a shared, cached reference-data value by both the <see cref="Enemies.EnemyTemplate"/> graph
    /// (across every <see cref="Enemies.Enemy"/> produced from a template) and a <see cref="Classes.Class"/>'s
    /// attribute fingerprint (#1126), so it is structurally immutable (init-only) — a consumer cannot corrupt
    /// the cache for every player (#547). Properties are <c>required</c> (matching its sibling reference models)
    /// so a distribution can never be silently built with the default enum-0 attribute and zero amounts.
    /// </summary>
    public sealed class AttributeDistribution
    {
        /// <summary>
        /// The attribute being distributed.
        /// </summary>
        public required EAttribute AttributeId { get; init; }

        /// <summary>
        /// The base amount of the attribute.
        /// </summary>
        public required decimal BaseAmount { get; init; }

        /// <summary>
        /// The amount of the attribute per level.
        /// </summary>
        public required decimal AmountPerLevel { get; init; }

        /// <summary>
        /// Creates a new <see cref="AttributeModifier"/> based on the given <paramref name="level"/>.
        /// </summary>
        /// <param name="level"></param>
        /// <returns></returns>
        public AttributeModifier GetDistributionModifier(int level)
        {
            return new AttributeModifier
            {
                Attribute = AttributeId,
                Amount = (double)(BaseAmount + (AmountPerLevel * level)),
                Type = EModifierType.Additive,
                Source = EAttributeModifierSource.AttributeDistribution,
            };
        }
    }
}
