using Game.Core;
using Game.Core.Attributes;
using Game.Core.Attributes.Modifiers;
using Game.Core.Proficiencies;
using Xunit;

namespace Game.Core.Tests.Attributes
{
    /// <summary>
    /// Parity guard for the proficiency attribute-bonus pipeline (spike #982 area E) — the one frontend↔backend
    /// parity surface the proficiency system adds. Every scenario here MUST be mirrored — with identical inputs
    /// (the same name, allocations, authored level payouts, and player level) and identical expected attribute
    /// values — in the frontend suite <c>UI/src/tests/lib/battle/proficiency-bonus-parity.test.ts</c>.
    /// <para>
    /// A proficiency's payouts (<see cref="ProficiencyModifier"/>) become <see cref="AttributeModifier"/>s with
    /// a <see cref="EAttributeModifierSource.Proficiency"/> source and compose through the same
    /// <see cref="AttributeCollection"/> path as stat allocations and gear, so the bonus participates in the
    /// additive-then-multiplicative ordering identically on both sides. Most scenarios assert on the core
    /// attributes the bonuses land on directly (stable literals, independent of the derived-stat coefficients
    /// the <see cref="BattleAttributesParityTests"/> already pin). The <c>maxHealthDerivedAdditive</c> scenario
    /// deliberately lands on a derived attribute that carries a static additive base, to pin the additive
    /// accumulation order of the proficiency bonus relative to the static modifiers (the divergence behind
    /// #1189); its expectation moves only if those base coefficients are retuned.
    /// </para>
    /// <para>
    /// Values are asserted <b>bit-exactly</b> (no tolerance): this is an anti-cheat parity surface, so the two
    /// simulators must agree to the last bit, and a loose tolerance is exactly what let the #1189 ordering
    /// divergence go unnoticed.
    /// </para>
    /// </summary>
    public class ProficiencyBonusParityTests
    {
        /// <summary>One payout level: the level at which it is granted and its attribute bonuses.</summary>
        public sealed record LevelSpec(int Level, (EAttribute Attribute, EModifierType Type, double Amount)[] Modifiers);

        /// <summary>
        /// A single proficiency-bonus scenario: the base stat allocations, the proficiency's authored payout
        /// levels, the player's current level in it, and the expected composed value of each asserted attribute.
        /// </summary>
        public sealed record Scenario(
            (EAttribute Attribute, double Amount)[] Allocations,
            LevelSpec[] Levels,
            int PlayerLevel,
            (EAttribute Attribute, double Expected)[] Expected);

        /// <summary>The shared scenario table, mirrored row-for-row by the frontend suite.</summary>
        public static readonly IReadOnlyDictionary<string, Scenario> Scenarios =
            new Dictionary<string, Scenario>
            {
                // Cumulative additive: the level-1 and level-2 Strength payouts both apply at level 2, the
                // far-off level-5 payout does not. Strength = 0 (alloc) + 4 + 6 = 10.
                ["cumulativeAdditive"] = new Scenario(
                    Allocations: [],
                    Levels:
                    [
                        new LevelSpec(1, [(EAttribute.Strength, EModifierType.Additive, 4)]),
                        new LevelSpec(2, [(EAttribute.Strength, EModifierType.Additive, 6)]),
                        new LevelSpec(5, [(EAttribute.Strength, EModifierType.Additive, 100)]),
                    ],
                    PlayerLevel: 2,
                    Expected: [(EAttribute.Strength, 10)]),

                // Additive then multiplicative: the proficiency additive sums with the allocation before the
                // proficiency multiplicative scales the total. Strength = (10 + 5) × 1.5 = 22.5.
                ["additiveThenMultiplicative"] = new Scenario(
                    Allocations: [(EAttribute.Strength, 10)],
                    Levels:
                    [
                        new LevelSpec(1, [(EAttribute.Strength, EModifierType.Additive, 5)]),
                        new LevelSpec(2, [(EAttribute.Strength, EModifierType.Multiplicative, 1.5)]),
                    ],
                    PlayerLevel: 2,
                    Expected: [(EAttribute.Strength, 22.5)]),

                // Below every payout: a player under the first payout level gets no bonus, so the attribute is
                // its allocation alone. Strength = 7.
                ["belowEveryPayout"] = new Scenario(
                    Allocations: [(EAttribute.Strength, 7)],
                    Levels: [new LevelSpec(3, [(EAttribute.Strength, EModifierType.Additive, 5)])],
                    PlayerLevel: 2,
                    Expected: [(EAttribute.Strength, 7)]),

                // Multiple attributes from one payout level, each landing on its own attribute.
                ["multiAttributePayout"] = new Scenario(
                    Allocations: [],
                    Levels:
                    [
                        new LevelSpec(1,
                        [
                            (EAttribute.Strength, EModifierType.Additive, 3),
                            (EAttribute.Endurance, EModifierType.Additive, 8),
                        ]),
                    ],
                    PlayerLevel: 1,
                    Expected: [(EAttribute.Strength, 3), (EAttribute.Endurance, 8)]),

                // Additive bonus on a DERIVED attribute (MaxHealth = 50 + 20·Endurance + 5·Strength). MaxHealth
                // carries a static additive base, so the proficiency additive must accumulate in the same order
                // relative to those statics as the frontend. The allocations (Endurance 34, Strength 59) and
                // bonus (3.14) make that order observable in the last bits: MaxHealth = 3.14 + 50 + 680 + 295 =
                // 1028.1399999999999, distinct from the "statics first" order (1028.14) past the 10th decimal.
                // The expectation is the exact double the canonical order produces, so a regression to the old
                // frontend ordering fails this row.
                ["maxHealthDerivedAdditive"] = new Scenario(
                    Allocations: [(EAttribute.Endurance, 34), (EAttribute.Strength, 59)],
                    Levels: [new LevelSpec(1, [(EAttribute.MaxHealth, EModifierType.Additive, 3.14)])],
                    PlayerLevel: 1,
                    Expected: [(EAttribute.MaxHealth, 1028.1399999999999)]),
            };

        public static IEnumerable<object[]> ScenarioNames =>
            Scenarios.Keys.Select(name => new object[] { name });

        [Theory]
        [MemberData(nameof(ScenarioNames))]
        public void Parity_Scenario_ComposesProficiencyBonusesOntoCoreAttributes(string scenarioName)
        {
            var scenario = Scenarios[scenarioName];
            var proficiency = BuildProficiency(scenario.Levels);

            var modifiers = scenario.Allocations
                .Select(a => new AttributeModifier
                {
                    Attribute = a.Attribute,
                    Amount = a.Amount,
                    Type = EModifierType.Additive,
                    Source = EAttributeModifierSource.PlayerStatPoints,
                })
                .Concat(proficiency.ModifiersForLevel(scenario.PlayerLevel));

            var collection = new AttributeCollection(modifiers);

            foreach (var (attribute, expected) in scenario.Expected)
            {
                // Bit-exact, not a tolerance: this is an anti-cheat parity surface, so the two simulators must
                // agree to the last bit. A loose tolerance previously masked the ordering divergence in #1189.
                Assert.Equal(expected, collection[attribute]);
            }
        }

        private static Proficiency BuildProficiency(LevelSpec[] levels) => new()
        {
            Id = 0,
            Name = "Test",
            Description = string.Empty,
            PathId = 0,
            PathOrdinal = 0,
            MaxLevel = 10,
            BaseXp = 100,
            XpGrowth = 2,
            SeedSkillId = null,
            PrerequisiteIds = [],
            Levels = levels.Select(l => new ProficiencyLevel
            {
                Level = l.Level,
                Modifiers = l.Modifiers
                    .Select(m => new ProficiencyModifier { Attribute = m.Attribute, ModifierType = m.Type, Amount = m.Amount })
                    .ToList(),
                RewardSkillId = null,
            }).ToList(),
        };
    }
}
