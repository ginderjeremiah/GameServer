using Game.Core.Attributes;
using Game.Core.Attributes.Modifiers;
using Xunit;

namespace Game.Core.Tests.Attributes
{
    /// <summary>
    /// Parity guard for the shared derived-stat formulas. Every scenario here MUST be mirrored —
    /// with identical inputs (the same name and core-attribute allocations) — in the frontend suite
    /// <c>UI/src/tests/lib/battle/battle-attributes.test.ts</c>, just as the battle-simulation and
    /// progression parity suites share a single named scenario table row-for-row.
    /// <para>
    /// The expected derived value is NOT a re-hardcoded literal: it is computed from the single source
    /// of truth — <see cref="StaticAttributeModifiers.All"/> on the backend, the codegen-generated
    /// <c>STATIC_ATTRIBUTE_MODIFIERS</c> on the frontend — by <see cref="ExpectedValue"/>. So a coefficient
    /// retune flows into both the production <see cref="AttributeCollection"/> path and the expectation,
    /// and the test pins that the two agree rather than a second hand-maintained copy of the numbers that
    /// could rot out of step.
    /// </para>
    /// </summary>
    public class BattleAttributesParityTests
    {
        /// <summary>
        /// A single derived-stat scenario: the core-attribute allocations that feed the collection
        /// and the derived attributes whose composed value is asserted against the single-sourced table.
        /// </summary>
        public sealed record DerivedStatScenario(
            (EAttribute Attribute, double Amount)[] Allocations,
            EAttribute[] DerivedAttributes);

        /// <summary>
        /// The shared scenario table, keyed by name so xUnit can drive a <see cref="TheoryAttribute"/>
        /// over the names; the test resolves the full scenario by name. Mirrored row-for-row by the
        /// frontend suite's <c>scenarios</c> table.
        /// </summary>
        public static readonly IReadOnlyDictionary<string, DerivedStatScenario> Scenarios =
            new Dictionary<string, DerivedStatScenario>
            {
                // MaxHealth = 50 (base) + 20·Endurance + 5·Strength
                ["maxHealth"] = new DerivedStatScenario(
                    [(EAttribute.Strength, 10), (EAttribute.Endurance, 20)],
                    [EAttribute.MaxHealth]),

                // Toughness = 2·Endurance (no base, Endurance-only)
                ["toughness"] = new DerivedStatScenario(
                    [(EAttribute.Endurance, 30)],
                    [EAttribute.Toughness]),

                // CooldownRecovery = 1 (base) + 0.004·Agility + 0.001·Dexterity
                ["cooldownRecovery"] = new DerivedStatScenario(
                    [(EAttribute.Agility, 20), (EAttribute.Dexterity, 10)],
                    [EAttribute.CooldownRecovery]),

                // CriticalChanceMultiplier is opt-in (crit rework #1425, per-skill base #1453): a base of 1 (like
                // CooldownRecovery) and no attribute derivation, so even a heavy Dexterity/Luck build sits at
                // exactly 1 — DEX/LUK no longer feed crit, and the enabler is a skill's own authored base chance,
                // not this attribute. The opt-in-multiplicative math is covered by
                // AttributeCollectionTests / attribute-collection.test.ts.
                ["criticalChanceMultiplier"] = new DerivedStatScenario(
                    [(EAttribute.Dexterity, 20), (EAttribute.Luck, 10)],
                    [EAttribute.CriticalChanceMultiplier]),

                // CriticalDamage = 1.5 (base) + 0.0025·Luck
                ["criticalDamage"] = new DerivedStatScenario(
                    [(EAttribute.Luck, 20)],
                    [EAttribute.CriticalDamage]),

                // DodgeChance = 0.001·Agility (no base)
                ["dodgeChance"] = new DerivedStatScenario(
                    [(EAttribute.Agility, 20)],
                    [EAttribute.DodgeChance]),

                // With no allocations every derived stat collapses to just its base: the three with a base carry
                // it (MaxHealth 50, CriticalDamage 1.5, CriticalChanceMultiplier 1), the pure-derived stats
                // (Toughness, CooldownRecovery's coefficients aside, and DodgeChance) are 0/base. DamageReflection
                // is authored-only (no static modifier at all), so it is 0 here too.
                ["zeroBaseStats"] = new DerivedStatScenario(
                    [],
                    [
                        EAttribute.MaxHealth, EAttribute.Toughness, EAttribute.CooldownRecovery,
                        EAttribute.CriticalDamage, EAttribute.DamageReflection,
                        EAttribute.CriticalChanceMultiplier, EAttribute.DodgeChance,
                    ]),
            };

        public static IEnumerable<object[]> ScenarioNames =>
            Scenarios.Keys.Select(name => new object[] { name });

        [Theory]
        [MemberData(nameof(ScenarioNames))]
        public void Parity_Scenario_ComposesDerivedStatsFromSingleSourcedCoefficients(string scenarioName)
        {
            var scenario = Scenarios[scenarioName];
            var collection = MakeCollection(scenario.Allocations);

            foreach (var attribute in scenario.DerivedAttributes)
            {
                var expected = ExpectedValue(attribute, scenario.Allocations);
                // Compared to 10 decimals: the static coefficients are fractional, so the additions
                // accumulate the usual binary-float error the frontend's toBeCloseTo also tolerates.
                Assert.Equal(expected, collection[attribute], 10);
            }
        }

        /// <summary>
        /// Composes the expected value of <paramref name="attribute"/> directly from
        /// <see cref="StaticAttributeModifiers.All"/> (the single source of truth) under the given
        /// core-attribute <paramref name="allocations"/>. Every static modifier is additive: a base value
        /// contributes its raw amount, a derived modifier contributes <c>amount × allocation[source]</c>
        /// (an allocated core attribute carries no further modifiers, so its final value is its raw amount).
        /// This is the same reduction <see cref="AttributeCollection"/> performs, so it can't diverge from
        /// the production path on a coefficient change.
        /// </summary>
        private static double ExpectedValue(EAttribute attribute, (EAttribute Attribute, double Amount)[] allocations)
        {
            var allocated = allocations.ToDictionary(a => a.Attribute, a => a.Amount);
            var expected = 0.0;
            foreach (var modifier in StaticAttributeModifiers.All)
            {
                if (modifier.Attribute != attribute)
                {
                    continue;
                }

                Assert.Equal(EModifierType.Additive, modifier.Type);
                expected += modifier.Source is EAttributeModifierSource.Derived
                    ? modifier.Amount * allocated.GetValueOrDefault(modifier.DerivedSource)
                    : modifier.Amount;
            }

            return expected;
        }

        private static AttributeCollection MakeCollection(params (EAttribute Attribute, double Amount)[] allocations)
        {
            var modifiers = allocations.Select(a => new AttributeModifier
            {
                Attribute = a.Attribute,
                Amount = a.Amount,
                Type = EModifierType.Additive,
                Source = EAttributeModifierSource.PlayerStatPoints,
            });

            return new AttributeCollection(modifiers);
        }
    }
}
