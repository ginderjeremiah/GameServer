using Game.Core.Attributes.Modifiers;

namespace Game.Core.Attributes
{
    /// <summary>
    /// Represents the distribution of an attribute. Part of the shared, cached <see cref="Enemies.EnemyTemplate"/>
    /// graph (reused by reference across every <see cref="Enemies.Enemy"/> produced from a template) and
    /// therefore structurally immutable (init-only) so a consumer cannot corrupt the cache for every player (#547).
    /// </summary>
    public class AttributeDistribution
    {
        /// <summary>
        /// The attribute being distributed.
        /// </summary>
        public EAttribute AttributeId { get; init; }

        /// <summary>
        /// The base amount of the attribute.
        /// </summary>
        public decimal BaseAmount { get; init; }

        /// <summary>
        /// The amount of the attribute per level.
        /// </summary>
        public decimal AmountPerLevel { get; init; }

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
