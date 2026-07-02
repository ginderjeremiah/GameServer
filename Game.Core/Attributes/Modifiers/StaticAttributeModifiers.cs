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

            // Toughness = 2·Endurance (no base, Endurance-only). It feeds the diminishing mitigation curve
            // (Toughness / (Toughness + K·attackerLevel)), so a non-Endurance build simply has no Toughness and
            // leans on Dodge/offense instead — the archetype split (spike #1330): Endurance → Toughness curve,
            // Agility → Dodge. The coefficient is a strawman to tune during balancing.
            new() { Attribute = Toughness, Amount = 2.0, Source = Derived, DerivedSource = Endurance, Type = Additive },

            // MaxHealth = 50 (base) + 20·Endurance + 5·Strength
            new() { Attribute = MaxHealth, Amount = 50.0, Source = BaseValue, Type = Additive },
            new() { Attribute = MaxHealth, Amount = 20.0, Source = Derived, DerivedSource = Endurance, Type = Additive },
            new() { Attribute = MaxHealth, Amount = 5.0, Source = Derived, DerivedSource = Strength, Type = Additive },

            // The remaining chance-based combat attributes. All use the decimal convention — a chance is
            // compared directly against the [0,1) battle RNG draw — and the coefficients are a conservative
            // strawman expected to be tuned during balancing. Two of them are deliberately NOT derived from a
            // core attribute here:
            //   • CriticalChanceMultiplier is opt-in (crit rework #1425, per-skill base #1453): the ENABLER is a
            //     skill's own authored CriticalChance (0 by default, so an un-authored skill never crits) — this
            //     attribute is only the base-1 MULTIPLIER scaling that per-skill base, exactly like
            //     CooldownRecovery scales the tick rate. A base of 1 (not 0) is deliberate: a committed skill
            //     still crits at its own authored rate with zero further investment, and a Precision/gear/mod
            //     bonus scales it up (or a debuff below 1). This makes crit a committed per-skill build identity
            //     rather than a stat every Dexterity/Luck build accrues for free. CriticalDamage keeps its Luck
            //     derivation + base below: inert without a crit, it stays opt-in-gated and pays off the moment a
            //     skill with a base chance fires.
            //   • DamageReflection (spike #1330) is authored-only — granted by gear/mod/proficiency/class rather
            //     than derived from a core attribute — so it too has no entry here (base 0 everywhere).
            // DodgeChance, by contrast, stays Agility-derived (its entry is last, below), so evasion is live
            // from raw allocations; the value is also grantable by gear/item-mods/skill-effects.
            new() { Attribute = CriticalChanceMultiplier, Amount = 1.0, Source = BaseValue, Type = Additive },

            // CriticalDamage = 1.5 (base) + 0.0025·Luck. A base-1.5 multiplier read directly (like
            // CooldownRecovery), so a crit is worth ×1.5 before any crit-damage gear and a ×2 modifier doubles it.
            new() { Attribute = CriticalDamage, Amount = 1.5, Source = BaseValue, Type = Additive },
            new() { Attribute = CriticalDamage, Amount = 0.0025, Source = Derived, DerivedSource = Luck, Type = Additive },

            // DodgeChance = 0.001·Agility (no base).
            new() { Attribute = DodgeChance, Amount = 0.001, Source = Derived, DerivedSource = Agility, Type = Additive },
        ];
    }
}
