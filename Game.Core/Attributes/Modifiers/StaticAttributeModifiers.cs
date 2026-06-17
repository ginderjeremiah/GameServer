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
            // CooldownRecovery = 1 (base) + 0.004·Agility + 0.001·Dexterity. The attribute is the cooldown
            // multiplier read directly (a base-1 multiplier, so a ×2 modifier genuinely doubles charge speed),
            // hence the base 1 and the derived coefficients scaled ÷100 from the legacy 1 + CDR/100 form
            // (AGI 20, DEX 10 → 1.09, identical to before the rebase).
            new() { Attribute = CooldownRecovery, Amount = 1.0, Source = BaseValue, Type = Additive },
            new() { Attribute = CooldownRecovery, Amount = 0.004, Source = Derived, DerivedSource = Agility, Type = Additive },
            new() { Attribute = CooldownRecovery, Amount = 0.001, Source = Derived, DerivedSource = Dexterity, Type = Additive },

            // Defense = 2 (base) + 1·Endurance + 0.5·Agility
            new() { Attribute = Defense, Amount = 2.0, Source = BaseValue, Type = Additive },
            new() { Attribute = Defense, Amount = 1.0, Source = Derived, DerivedSource = Endurance, Type = Additive },
            new() { Attribute = Defense, Amount = 0.5, Source = Derived, DerivedSource = Agility, Type = Additive },

            // MaxHealth = 50 (base) + 20·Endurance + 5·Strength
            new() { Attribute = MaxHealth, Amount = 50.0, Source = BaseValue, Type = Additive },
            new() { Attribute = MaxHealth, Amount = 20.0, Source = Derived, DerivedSource = Endurance, Type = Additive },
            new() { Attribute = MaxHealth, Amount = 5.0, Source = Derived, DerivedSource = Strength, Type = Additive },

            // The crit/dodge/block derivations make the mechanic live from raw allocations (the values are
            // also grantable by gear/item-mods/skill-effects through the normal modifier path). All use the
            // decimal convention — a chance is compared directly against the [0,1) battle RNG draw — and the
            // coefficients are a conservative strawman expected to be tuned during balancing.

            // CriticalChance = 0.002·Dexterity + 0.001·Luck (no base, so 0 until something feeds it).
            new() { Attribute = CriticalChance, Amount = 0.002, Source = Derived, DerivedSource = Dexterity, Type = Additive },
            new() { Attribute = CriticalChance, Amount = 0.001, Source = Derived, DerivedSource = Luck, Type = Additive },

            // CriticalDamage = 1.5 (base) + 0.0025·Luck. A base-1.5 multiplier read directly (like
            // CooldownRecovery), so a crit is worth ×1.5 before any crit-damage gear and a ×2 modifier doubles it.
            new() { Attribute = CriticalDamage, Amount = 1.5, Source = BaseValue, Type = Additive },
            new() { Attribute = CriticalDamage, Amount = 0.0025, Source = Derived, DerivedSource = Luck, Type = Additive },

            // DodgeChance = 0.001·Agility (no base).
            new() { Attribute = DodgeChance, Amount = 0.001, Source = Derived, DerivedSource = Agility, Type = Additive },

            // BlockChance = 0.002·Endurance (no base).
            new() { Attribute = BlockChance, Amount = 0.002, Source = Derived, DerivedSource = Endurance, Type = Additive },

            // BlockReduction = 2 (base) + 0.5·Endurance. A flat reduction (like Defense), subtracted in the same clamp on a block.
            new() { Attribute = BlockReduction, Amount = 2.0, Source = BaseValue, Type = Additive },
            new() { Attribute = BlockReduction, Amount = 0.5, Source = Derived, DerivedSource = Endurance, Type = Additive },
        ];
    }
}
