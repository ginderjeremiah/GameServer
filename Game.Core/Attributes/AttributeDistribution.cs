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
        /// Creates a new <see cref="AttributeModifier"/> for the given <paramref name="level"/>:
        /// <c>BaseAmount + AmountPerLevel × level</c>.
        /// <para>
        /// The arithmetic is done in <see cref="double"/> (each authored <see cref="decimal"/> operand is cast
        /// first), <b>not</b> in decimal-then-cast, so it is bit-identical to the frontend, which recomputes
        /// the same expression in IEEE-754 double from the JSON-serialized operands (`class-modifiers.ts`,
        /// `enemy-attributes.ts`). A fractional <see cref="AmountPerLevel"/>/<see cref="BaseAmount"/> would
        /// otherwise diverge in the last bits between decimal and double arithmetic, and the anti-cheat replay
        /// compares attributes with no tolerance — so this keeps both the class locked base and the enemy
        /// distribution exact on the parity surface for any authored value, not just integers.
        /// </para>
        /// </summary>
        public AttributeModifier GetDistributionModifier(int level)
        {
            return new AttributeModifier
            {
                Attribute = AttributeId,
                Amount = (double)BaseAmount + (double)AmountPerLevel * level,
                Type = EModifierType.Additive,
                Source = EAttributeModifierSource.AttributeDistribution,
            };
        }
    }
}
