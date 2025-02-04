using Game.Core.Attributes.Modifiers;

namespace Game.Core.Attributes
{
    /// <summary>
    /// Represents the distribution of an attribute.
    /// </summary>
    public class AttributeDistribution
    {
        /// <summary>
        /// The attribute being distributed.
        /// </summary>
        public EAttribute AttributeId { get; set; }

        /// <summary>
        /// The base amount of the attribute.
        /// </summary>
        public decimal BaseAmount { get; set; }

        /// <summary>
        /// The amount of the attribute per level.
        /// </summary>
        public decimal AmountPerLevel { get; set; }

        /// <summary>
        /// Creates a new <see cref="AttributeModifier"/> based on the given <paramref name="level"/>.
        /// </summary>
        /// <param name="level"></param>
        /// <returns></returns>
        public AttributeModifier GetDistributionModifier(int level)
        {
            return new AttributeModifier
            {
                Attribute = Attribute,
                Amount = (double)(BaseAmount + (AmountPerLevel * level)),
                Type = EModifierType.Additive,
                Source = EAttributeModifierSource.AttributeDistribution,
            };
        }
    }
}
