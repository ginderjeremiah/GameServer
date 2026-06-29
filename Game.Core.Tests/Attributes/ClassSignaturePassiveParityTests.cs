using Game.Core.Attributes;
using Game.Core.Attributes.Modifiers;
using Game.Core.Classes;
using Xunit;

namespace Game.Core.Tests.Attributes
{
    /// <summary>
    /// Parity guard for the class signature-passive attribute pipeline (spike #1126 area E) — a frontend↔backend
    /// parity surface the class system adds. Every scenario here MUST be mirrored — with identical inputs (the
    /// same allocations, class attribute distributions, level, and passive) and identical expected attribute
    /// values — in the frontend suite <c>UI/src/tests/lib/battle/class-signature-passive-parity.test.ts</c>.
    /// <para>
    /// The passive resolves to a single <see cref="AttributeModifier"/> (<see cref="ClassSignaturePassive.GetModifier"/>)
    /// — flat (<c>Amount</c>) or attribute-scaled (<c>Amount + ScalingAmount × value(ScalingAttribute)</c>) — and
    /// is composed into the battler <b>last</b>: after the free pool, the class locked base, and the static engine
    /// modifiers, reading the fully-resolved value of its scaling attribute (the snapshot state a V1 passive sees,
    /// like a skill effect reading its caster, so it never depends on itself). Because float addition is not
    /// associative, the passive must land in the same place in the per-attribute apply order on both sides; the
    /// production paths mirror this (backend <see cref="Battle.BattleSnapshot.ToBattler"/> adds it after building
    /// the collection; the frontend battle engine adds it after building the battler).
    /// </para>
    /// <para>
    /// Values are asserted <b>bit-exactly</b> (no tolerance): this is an anti-cheat parity surface, so the two
    /// simulators must agree to the last bit. <see cref="ClassSignaturePassive.GetModifier"/> does the
    /// <c>Amount + ScalingAmount × value</c> arithmetic in <see cref="double"/> (each authored <see cref="decimal"/>
    /// operand cast first), matching the frontend — the <c>fractionalScaling</c> scenario pins exactly a case
    /// (scaling amount <c>0.1</c>, source <c>3</c>) decimal-then-cast would have diverged on (<c>0.3</c> vs the
    /// double <c>0.30000000000000004</c>).
    /// </para>
    /// </summary>
    public class ClassSignaturePassiveParityTests
    {
        /// <summary>One locked-base distribution: the attribute and its base + per-level amounts.</summary>
        public sealed record DistributionSpec(EAttribute Attribute, decimal BaseAmount, decimal AmountPerLevel);

        /// <summary>The signature passive under test: its attribute, flat amount, optional scaling source and
        /// per-point amount, and modifier type.</summary>
        public sealed record PassiveSpec(
            EAttribute Attribute, decimal Amount, EAttribute? ScalingAttribute, decimal ScalingAmount, EModifierType ModifierType);

        /// <summary>
        /// A single signature-passive scenario: the free-pool allocations, the class's attribute distributions,
        /// the character's level, the passive, and the expected composed value of each asserted attribute.
        /// </summary>
        public sealed record Scenario(
            (EAttribute Attribute, double Amount)[] Allocations,
            DistributionSpec[] Distributions,
            int Level,
            PassiveSpec Passive,
            (EAttribute Attribute, double Expected)[] Expected);

        /// <summary>The shared scenario table, mirrored row-for-row by the frontend suite.</summary>
        public static readonly IReadOnlyDictionary<string, Scenario> Scenarios =
            new Dictionary<string, Scenario>
            {
                // Flat additive passive on a core attribute, on top of the free pool and the locked base.
                // Strength = 5 (alloc) + (3 + 1 × 2) locked base + 4 (passive) = 14.
                ["flatAdditiveCore"] = new Scenario(
                    Allocations: [(EAttribute.Strength, 5)],
                    Distributions: [new DistributionSpec(EAttribute.Strength, 3m, 1m)],
                    Level: 2,
                    Passive: new PassiveSpec(EAttribute.Strength, 4m, null, 0m, EModifierType.Additive),
                    Expected: [(EAttribute.Strength, 14)]),

                // Passive scaling off a CORE attribute, landing on a DERIVED one.
                // Endurance = 5 (alloc) + (4 + 3 × 2) locked base = 15. Passive on Toughness = 2 + 0.5 × 15 = 9.5.
                // Toughness (static 2·Endurance) = 30 + 9.5 = 39.5 — the passive accumulates after the statics,
                // the same order on both sides.
                ["scaledOffCoreOntoDerived"] = new Scenario(
                    Allocations: [(EAttribute.Endurance, 5)],
                    Distributions: [new DistributionSpec(EAttribute.Endurance, 4m, 3m)],
                    Level: 2,
                    Passive: new PassiveSpec(EAttribute.Toughness, 2m, EAttribute.Endurance, 0.5m, EModifierType.Additive),
                    Expected: [(EAttribute.Toughness, 39.5), (EAttribute.Endurance, 15)]),

                // Fractional scaling: Luck = 0 + 0.1 × Strength(3). In double this is 0.30000000000000004; the
                // decimal-then-cast path would produce 0.3, flagging the replay. Pins that both sides share double
                // arithmetic so a fractional scaling amount is exact.
                ["fractionalScaling"] = new Scenario(
                    Allocations: [(EAttribute.Strength, 3)],
                    Distributions: [],
                    Level: 1,
                    Passive: new PassiveSpec(EAttribute.Luck, 0m, EAttribute.Strength, 0.1m, EModifierType.Additive),
                    Expected: [(EAttribute.Luck, 0.30000000000000004), (EAttribute.Strength, 3)]),

                // Passive scaling off a DERIVED attribute: it must read the scaling source's fully-assembled value
                // (statics included). MaxHealth = 50 + 20 × Endurance(2) + 5 × Strength(3) = 105. Luck = 0 + 0.5 ×
                // 105 = 52.5.
                ["scaledOffDerived"] = new Scenario(
                    Allocations: [(EAttribute.Endurance, 2), (EAttribute.Strength, 3)],
                    Distributions: [],
                    Level: 1,
                    Passive: new PassiveSpec(EAttribute.Luck, 0m, EAttribute.MaxHealth, 0.5m, EModifierType.Additive),
                    Expected: [(EAttribute.Luck, 52.5), (EAttribute.MaxHealth, 105)]),

                // Self-scaling reads the PRE-passive value (snapshot state), never itself. Strength(pre) = 10
                // (alloc). Passive = 0 + 0.5 × 10 = 5, baked once. Strength = 10 + 5 = 15.
                ["selfScaling"] = new Scenario(
                    Allocations: [(EAttribute.Strength, 10)],
                    Distributions: [],
                    Level: 1,
                    Passive: new PassiveSpec(EAttribute.Strength, 0m, EAttribute.Strength, 0.5m, EModifierType.Additive),
                    Expected: [(EAttribute.Strength, 15)]),

                // Multiplicative passive applies AFTER the additive subtotal, the same order on both sides.
                // MaxHealth (static 50 + 20·Endurance(5)) = 150, then × 1.5 = 225.
                ["multiplicative"] = new Scenario(
                    Allocations: [(EAttribute.Endurance, 5)],
                    Distributions: [],
                    Level: 1,
                    Passive: new PassiveSpec(EAttribute.MaxHealth, 1.5m, null, 0m, EModifierType.Multiplicative),
                    Expected: [(EAttribute.MaxHealth, 225), (EAttribute.Endurance, 5)]),
            };

        public static IEnumerable<object[]> ScenarioNames =>
            Scenarios.Keys.Select(name => new object[] { name });

        [Theory]
        [MemberData(nameof(ScenarioNames))]
        public void Parity_Scenario_ComposesClassSignaturePassiveOntoAttributes(string scenarioName)
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

            // Compose the passive last, exactly as BattleSnapshot.ToBattler does: read the scaling source off the
            // fully-assembled collection (free pool + locked base + statics), then add the resolved modifier.
            var passive = new ClassSignaturePassive
            {
                Attribute = scenario.Passive.Attribute,
                Amount = scenario.Passive.Amount,
                ScalingAttribute = scenario.Passive.ScalingAttribute,
                ScalingAmount = scenario.Passive.ScalingAmount,
                ModifierType = scenario.Passive.ModifierType,
            };
            collection.AddModifier(passive.GetModifier(collection.GetAttributeValue));

            foreach (var (attribute, expected) in scenario.Expected)
            {
                // Bit-exact, not a tolerance: this is an anti-cheat parity surface, so the two simulators must
                // agree to the last bit (the ordering divergence pattern fixed for proficiency in #1189).
                Assert.Equal(expected, collection[attribute]);
            }
        }
    }
}
