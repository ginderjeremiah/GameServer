using Game.Core.Attributes.Modifiers;

namespace Game.Core.Classes
{
    /// <summary>
    /// A class's signature passive — the durable combat-identity bonus fed through the attribute pipeline
    /// (see the class-system spike, <c>docs/spikes/1126-class-system.md</c>). Flat or attribute-scaled
    /// (<c>Amount + ScalingAttribute × ScalingAmount</c>), mirroring skill-effect scaling. Structurally
    /// immutable so the shared cached <see cref="Class"/> graph can be reused by reference. It is composed
    /// into the battler at assembly (spike #1126 area E) via <see cref="GetModifier"/>.
    /// </summary>
    public sealed class ClassSignaturePassive
    {
        public required EAttribute Attribute { get; init; }
        public required decimal Amount { get; init; }
        public EAttribute? ScalingAttribute { get; init; }
        public required decimal ScalingAmount { get; init; }
        public required EModifierType ModifierType { get; init; }

        /// <summary>
        /// Resolves this passive into a single <see cref="AttributeModifier"/> tagged
        /// <see cref="EAttributeModifierSource.Class"/>. A purely flat passive contributes <see cref="Amount"/>;
        /// an attribute-scaled one adds <c>ScalingAmount × resolveScalingValue(ScalingAttribute)</c>, reading the
        /// scaling attribute's <b>already-assembled</b> value — the same snapshot-state read a skill effect does
        /// off its caster (<see cref="Battle.BattleContext.ApplySkillEffect"/>), so a V1 passive never depends on
        /// itself. The arithmetic is done in <see cref="double"/> (each authored <see cref="decimal"/> operand is
        /// cast first), <b>not</b> decimal-then-cast, so it is bit-identical to the frontend mirror
        /// (<c>class-modifiers.ts</c>), which recomputes the same expression in IEEE-754 double from the
        /// JSON-serialized operands — the anti-cheat replay compares attributes with no tolerance.
        /// </summary>
        public AttributeModifier GetModifier(Func<EAttribute, double> resolveScalingValue)
        {
            var amount = (double)Amount;
            if (ScalingAttribute is EAttribute scalingAttribute)
            {
                amount += (double)ScalingAmount * resolveScalingValue(scalingAttribute);
            }

            return new AttributeModifier
            {
                Attribute = Attribute,
                Amount = amount,
                Type = ModifierType,
                Source = EAttributeModifierSource.Class,
            };
        }
    }
}
