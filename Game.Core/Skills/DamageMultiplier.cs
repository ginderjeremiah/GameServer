namespace Game.Core.Skills
{
    /// <summary>
    /// An attribute-scaled contribution to a skill's raw damage: the skill adds
    /// <see cref="Amount"/> × the active battler's <see cref="Attribute"/> value to its base damage
    /// (see <see cref="Battle.BattleSkill.CalculateDamage"/>). This is a flat <c>attribute × amount</c>
    /// term, <b>not</b> an <see cref="Attributes.Modifiers.AttributeModifier"/>: it never enters the
    /// attribute-composition graph, so it deliberately carries no modifier <c>Type</c>/<c>Source</c>.
    /// It mirrors the frontend's dedicated <c>IAttributeMultiplier</c> shape, keeping skill damage
    /// scaling represented identically on both sides of the battle-parity boundary.
    /// </summary>
    public sealed class DamageMultiplier
    {
        /// <summary>The attribute whose value scales this contribution.</summary>
        public required EAttribute Attribute { get; init; }

        /// <summary>The per-point coefficient applied to the attribute's value.</summary>
        public required double Amount { get; init; }
    }
}
