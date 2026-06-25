using Game.Core.Players;
using Game.Core.TestInfrastructure.Builders;
using Xunit;

namespace Game.Core.Tests.Players
{
    /// <summary>
    /// Cross-implementation parity matrix for the deterministic level/exp progression logic. Every
    /// scenario here MUST be mirrored — with identical inputs and identical expected
    /// level/exp/stat-points — in the frontend suite
    /// <c>UI/src/tests/lib/engine/player/player-progression-parity.test.ts</c>. The exp curve
    /// (<see cref="GameConstants.ExpPerLevel"/>) and the per-level stat-point award
    /// (<see cref="GameConstants.StatPointsPerLevel"/>) are shared across the boundary (#304), so a change
    /// to either must keep both suites in step the same way the battle parity matrix does.
    ///
    /// The backend additionally clamps a single grant to <see cref="ServerGameConstants.MaxExpPerGrant"/>
    /// as a server-only anti-cheat backstop; the frontend deliberately does not (the server is
    /// authoritative). Every shared scenario therefore grants well below that ceiling so the two sides
    /// agree — the clamp itself is covered by the backend-only <c>PlayerTests.GrantExp_*</c> cases.
    /// </summary>
    public class PlayerProgressionParityTests
    {
        /// <summary>
        /// A single deterministic progression scenario: the starting player state plus the grant, and the
        /// exact level/exp/stat-points the grant must produce.
        /// </summary>
        public sealed record ProgressionScenario(
            int StartLevel, int StartExp, int StartStatPoints, int Grant,
            int ExpectedLevel, int ExpectedExp, int ExpectedStatPoints);

        // (startLevel, startExp, startStatPoints, grant) → (expectedLevel, expectedExp, expectedStatPoints)
        public static readonly IReadOnlyDictionary<string, ProgressionScenario> Scenarios =
            new Dictionary<string, ProgressionScenario>
            {
                // Stat points accrue at GameConstants.StatPointsPerLevel (the reduced free pool, currently 2)
                // per level gained; each scenario's starting stat points are (StartLevel - 1) × that rate.
                // Below the level threshold: exp accrues, no level-up, no stat points.
                ["belowThreshold"] = new(1, 0, 0, 50, 1, 50, 0),
                // Exactly at the threshold (>= 100) levels once with no carryover.
                ["exactThreshold"] = new(1, 0, 0, 100, 2, 0, 2),
                // One level with carryover exp.
                ["thresholdWithCarryover"] = new(1, 0, 0, 101, 2, 1, 2),
                // Spans two levels in one grant (100 + 200 = 300 to reach level 3).
                ["twoLevels"] = new(1, 0, 0, 301, 3, 1, 4),
                // Starts mid-level with existing exp and stat points: one more level, points accumulate.
                ["partialStartExp"] = new(2, 50, 2, 199, 3, 49, 4),
                // Multi-level from a higher level (thresholds 300 and 400 consumed; 500 not reached).
                ["multiLevelFromHigherLevel"] = new(3, 0, 4, 1000, 5, 300, 8),
                // A large but sub-clamp grant levels many times: thresholds 100..900 consumed, 500 left.
                ["largeGrantManyLevels"] = new(1, 0, 0, 5000, 10, 500, 18),
                // A grant just shy of a high-level threshold (10 * 100) does not level up.
                ["noLevelAtHighLevel"] = new(10, 0, 18, 999, 10, 999, 18),
            };

        public static IEnumerable<object[]> ScenarioNames =>
            Scenarios.Keys.Select(name => new object[] { name });

        [Theory]
        [MemberData(nameof(ScenarioNames))]
        public void Parity_Scenario_MatchesExpectedResult(string scenarioName)
        {
            var scenario = Scenarios[scenarioName];
            var player = MakePlayer(scenario.StartLevel, scenario.StartExp, scenario.StartStatPoints);

            player.GrantExp(scenario.Grant);

            Assert.Equal(scenario.ExpectedLevel, player.Level);
            Assert.Equal(scenario.ExpectedExp, player.Exp);
            Assert.Equal(scenario.ExpectedStatPoints, player.StatPoints.StatPointsGained);
        }

        private static Player MakePlayer(int level, int exp, int statPointsGained) =>
            new PlayerBuilder()
                .WithLevel(level)
                .WithExp(exp)
                .WithStatPointsGained(statPointsGained)
                .Build();
    }
}
