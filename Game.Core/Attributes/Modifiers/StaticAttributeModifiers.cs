using static Game.Core.EAttribute;
using static Game.Core.EAttributeModifierSource;
using static Game.Core.EModifierType;

namespace Game.Core.Attributes.Modifiers
{
    /// <summary>
    /// The engine's static attribute modifiers — the base values and derived formulas every
    /// <see cref="AttributeCollection"/> is built on top of.
    /// </summary>
    /// <remarks>
    /// <see cref="All"/> is the single ordered source of truth for these modifiers:
    /// <see cref="AttributeCollection"/> seeds itself from it, and the frontend's
    /// <c>STATIC_ATTRIBUTE_MODIFIERS</c> table is generated from it by <c>Game.Api.CodeGen</c>
    /// (rather than hand-mirrored), so the two implementations cannot silently drift.
    /// </remarks>
    public static class StaticAttributeModifiers
    {
        /// <summary>
        /// Every static modifier, in the order they are applied — which is also the order the
        /// generated frontend table and the attribute-breakdown screen present them in.
        /// </summary>
        public static IReadOnlyList<AttributeModifier> All { get; } =
        [
            // CooldownRecovery = 0.4·Agility + 0.1·Dexterity
            new() { Attribute = CooldownRecovery, Amount = 0.4, Source = Derived, DerivedSource = Agility, Type = Additive },
            new() { Attribute = CooldownRecovery, Amount = 0.1, Source = Derived, DerivedSource = Dexterity, Type = Additive },

            // Defense = 2 (base) + 1·Endurance + 0.5·Agility
            new() { Attribute = Defense, Amount = 2.0, Source = BaseValue, Type = Additive },
            new() { Attribute = Defense, Amount = 1.0, Source = Derived, DerivedSource = Endurance, Type = Additive },
            new() { Attribute = Defense, Amount = 0.5, Source = Derived, DerivedSource = Agility, Type = Additive },

            // MaxHealth = 50 (base) + 20·Endurance + 5·Strength
            new() { Attribute = MaxHealth, Amount = 50.0, Source = BaseValue, Type = Additive },
            new() { Attribute = MaxHealth, Amount = 20.0, Source = Derived, DerivedSource = Endurance, Type = Additive },
            new() { Attribute = MaxHealth, Amount = 5.0, Source = Derived, DerivedSource = Strength, Type = Additive },
        ];
    }
}
