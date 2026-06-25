using Game.Core.Attributes;
using Game.Core.Attributes.Modifiers;
using Xunit;

namespace Game.Core.Tests.Attributes
{
    /// <summary>
    /// Parity guard for the class locked-base attribute pipeline (spike #1126 area D) — a frontend↔backend
    /// parity surface the class system adds. Every scenario here MUST be mirrored — with identical inputs (the
    /// same allocations, class attribute distributions, and level) and identical expected attribute values — in
    /// the frontend suite <c>UI/src/tests/lib/battle/class-locked-base-parity.test.ts</c>.
    /// <para>
    /// A class's <see cref="AttributeDistribution"/>s become <see cref="AttributeModifier"/>s
    /// (<see cref="AttributeDistribution.GetDistributionModifier"/>, the same <c>BaseAmount + AmountPerLevel ×
    /// level</c> math an enemy's distribution uses) with an <see cref="EAttributeModifierSource.AttributeDistribution"/>
    /// source, and compose through the same <see cref="AttributeCollection"/> path as stat allocations and the
    /// proficiency bonuses — in the same place in the modifier order (with the base set, before the static
    /// engine modifiers), since floating-point addition is not associative. Most scenarios assert on the core
    /// attributes the locked base lands on directly; the <c>maxHealthDerivedAdditive</c> scenario lands on a
    /// derived attribute that carries a static additive base to pin the accumulation order of the locked base
    /// relative to the static modifiers.
    /// </para>
    /// <para>
    /// Values are asserted <b>bit-exactly</b> (no tolerance): this is an anti-cheat parity surface, so the two
    /// simulators must agree to the last bit. The leveled scenarios use integer base/per-level so the backend's
    /// decimal arithmetic and the frontend's double arithmetic agree exactly; the fractional MaxHealth term uses
    /// a zero per-level so the per-level multiplication introduces no decimal-vs-double divergence.
    /// </para>
    /// </summary>
    public class ClassLockedBaseParityTests
    {
        /// <summary>One locked-base distribution: the attribute and its base + per-level amounts.</summary>
        public sealed record DistributionSpec(EAttribute Attribute, decimal BaseAmount, decimal AmountPerLevel);

        /// <summary>
        /// A single locked-base scenario: the free-pool allocations, the class's attribute distributions, the
        /// character's level, and the expected composed value of each asserted attribute.
        /// </summary>
        public sealed record Scenario(
            (EAttribute Attribute, double Amount)[] Allocations,
            DistributionSpec[] Distributions,
            int Level,
            (EAttribute Attribute, double Expected)[] Expected);

        /// <summary>The shared scenario table, mirrored row-for-row by the frontend suite.</summary>
        public static readonly IReadOnlyDictionary<string, Scenario> Scenarios =
            new Dictionary<string, Scenario>
            {
                // Leveled base: the distribution scales with level. Strength = 0 (alloc) + (10 + 2 × 5) = 20.
                ["leveledBase"] = new Scenario(
                    Allocations: [],
                    Distributions: [new DistributionSpec(EAttribute.Strength, 10m, 2m)],
                    Level: 5,
                    Expected: [(EAttribute.Strength, 20)]),

                // Locked base composes additively with the free-pool allocation.
                // Endurance = 5 (alloc) + (4 + 3 × 2) = 15.
                ["basePlusAllocation"] = new Scenario(
                    Allocations: [(EAttribute.Endurance, 5)],
                    Distributions: [new DistributionSpec(EAttribute.Endurance, 4m, 3m)],
                    Level: 2,
                    Expected: [(EAttribute.Endurance, 15)]),

                // Multiple attributes from the fingerprint, each landing on its own attribute.
                // Strength = 3 + 0 × 4 = 3; Agility = 7 + 1 × 4 = 11.
                ["multiAttribute"] = new Scenario(
                    Allocations: [],
                    Distributions:
                    [
                        new DistributionSpec(EAttribute.Strength, 3m, 0m),
                        new DistributionSpec(EAttribute.Agility, 7m, 1m),
                    ],
                    Level: 4,
                    Expected: [(EAttribute.Strength, 3), (EAttribute.Agility, 11)]),

                // Additive locked base on a DERIVED attribute (MaxHealth = 50 + 20·Endurance + 5·Strength). The
                // locked base feeds Endurance (34) and Strength (59) plus a fractional MaxHealth term (3.14,
                // per-level 0 so no arithmetic divergence). MaxHealth carries a static additive base, so the
                // locked-base additive must accumulate in the same order relative to those statics as the
                // frontend: MaxHealth = 3.14 + 50 + 680 + 295 = 1028.1399999999999, distinct from the "statics
                // first" order (1028.14) past the 10th decimal.
                ["maxHealthDerivedAdditive"] = new Scenario(
                    Allocations: [],
                    Distributions:
                    [
                        new DistributionSpec(EAttribute.Endurance, 34m, 0m),
                        new DistributionSpec(EAttribute.Strength, 59m, 0m),
                        new DistributionSpec(EAttribute.MaxHealth, 3.14m, 0m),
                    ],
                    Level: 1,
                    Expected: [(EAttribute.MaxHealth, 1028.1399999999999)]),
            };

        public static IEnumerable<object[]> ScenarioNames =>
            Scenarios.Keys.Select(name => new object[] { name });

        [Theory]
        [MemberData(nameof(ScenarioNames))]
        public void Parity_Scenario_ComposesClassLockedBaseOntoAttributes(string scenarioName)
        {
            var scenario = Scenarios[scenarioName];

            var modifiers = scenario.Allocations
                .Select(a => new AttributeModifier
                {
                    Attribute = a.Attribute,
                    Amount = a.Amount,
                    Type = EModifierType.Additive,
                    Source = EAttributeModifierSource.PlayerStatPoints,
                })
                .Concat(scenario.Distributions.Select(distribution => new AttributeDistribution
                {
                    AttributeId = distribution.Attribute,
                    BaseAmount = distribution.BaseAmount,
                    AmountPerLevel = distribution.AmountPerLevel,
                }.GetDistributionModifier(scenario.Level)));

            var collection = new AttributeCollection(modifiers);

            foreach (var (attribute, expected) in scenario.Expected)
            {
                // Bit-exact, not a tolerance: this is an anti-cheat parity surface, so the two simulators must
                // agree to the last bit (the ordering divergence pattern fixed for proficiency in #1189).
                Assert.Equal(expected, collection[attribute]);
            }
        }
    }
}
