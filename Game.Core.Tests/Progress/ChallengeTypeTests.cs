using Game.Core;
using Game.Core.Progress;
using Xunit;

namespace Game.Core.Tests.Progress
{
    public class ChallengeTypeTests
    {
        [Theory]
        [InlineData(EChallengeType.EnemiesKilled, EStatisticType.EnemiesKilled)]
        [InlineData(EChallengeType.BossesDefeated, EStatisticType.BossesDefeated)]
        [InlineData(EChallengeType.ZonesCleared, EStatisticType.ZonesCleared)]
        [InlineData(EChallengeType.TimeTrial, EStatisticType.FastestVictory)]
        [InlineData(EChallengeType.DamageDealt, EStatisticType.DamageDealt)]
        [InlineData(EChallengeType.BattlesWon, EStatisticType.BattlesWon)]
        [InlineData(EChallengeType.SkillsUsed, EStatisticType.SkillsUsed)]
        public void StatisticBackedTypes_MapToExpectedStatisticType(EChallengeType type, EStatisticType expected)
        {
            var challengeType = new ChallengeType(type);

            Assert.Equal(type, challengeType.Id);
            Assert.NotNull(challengeType.StatisticType);
            Assert.Equal(expected, challengeType.StatisticType.Id);
        }

        [Fact]
        public void LevelReached_HasNoBackingStatisticType()
        {
            var challengeType = new ChallengeType(EChallengeType.LevelReached);

            Assert.Equal(EChallengeType.LevelReached, challengeType.Id);
            Assert.Null(challengeType.StatisticType);
        }

        [Fact]
        public void TimeTrial_UsesAtMostGoalComparison()
        {
            var challengeType = new ChallengeType(EChallengeType.TimeTrial);

            Assert.Equal(EChallengeGoalComparison.AtMost, challengeType.GoalComparison);
        }

        [Theory]
        [InlineData(EChallengeType.EnemiesKilled)]
        [InlineData(EChallengeType.BossesDefeated)]
        [InlineData(EChallengeType.ZonesCleared)]
        [InlineData(EChallengeType.LevelReached)]
        [InlineData(EChallengeType.DamageDealt)]
        [InlineData(EChallengeType.BattlesWon)]
        [InlineData(EChallengeType.SkillsUsed)]
        public void AccumulatingTypes_UseAtLeastGoalComparison(EChallengeType type)
        {
            var challengeType = new ChallengeType(type);

            Assert.Equal(EChallengeGoalComparison.AtLeast, challengeType.GoalComparison);
        }

        [Fact]
        public void GoalComparison_IsDerivedFromBackingStatisticAggregation()
        {
            // The single domain fact ("this statistic is minimized") must drive both the recording mutator
            // and the challenge comparison. This pins the invariant the issue (#839) exists to enforce:
            // a min-aggregated backing statistic yields AtMost; anything else (including a challenge with no
            // backing statistic) yields AtLeast — so the two encodings can never silently disagree.
            foreach (var challengeType in ChallengeType.GetAll())
            {
                var isMinAggregated = challengeType.StatisticType?.AggregationKind == EAggregationKind.Min;
                var expected = isMinAggregated
                    ? EChallengeGoalComparison.AtMost
                    : EChallengeGoalComparison.AtLeast;

                Assert.Equal(expected, challengeType.GoalComparison);
            }
        }

        [Fact]
        public void EveryMinAggregatedStatisticChallengeUsesAtMost()
        {
            // Stated directionally as the issue asks: every challenge backed by a minimized statistic must
            // compare AtMost, and every AtMost challenge must be backed by a minimized statistic — there is
            // no other source of an AtMost comparison.
            foreach (var challengeType in ChallengeType.GetAll())
            {
                var isMinAggregated = challengeType.StatisticType?.AggregationKind == EAggregationKind.Min;
                var isAtMost = challengeType.GoalComparison == EChallengeGoalComparison.AtMost;

                Assert.Equal(isMinAggregated, isAtMost);
            }
        }

        [Fact]
        public void EveryChallengeTypeMapsToItsIntendedGoalComparison()
        {
            // The two tests above pin the derivation rule (AtMost iff Min-aggregated), but that rule is
            // self-referential: it can't catch a future Min-backed statistic whose challenge should still
            // accumulate "at least". This pins the concrete intended direction per type, so adding a new
            // challenge type (or flipping a backing statistic to Min) forces a deliberate decision here
            // rather than silently inheriting an AtMost goal.
            var expected = new Dictionary<EChallengeType, EChallengeGoalComparison>
            {
                [EChallengeType.EnemiesKilled] = EChallengeGoalComparison.AtLeast,
                [EChallengeType.BossesDefeated] = EChallengeGoalComparison.AtLeast,
                [EChallengeType.ZonesCleared] = EChallengeGoalComparison.AtLeast,
                [EChallengeType.LevelReached] = EChallengeGoalComparison.AtLeast,
                [EChallengeType.TimeTrial] = EChallengeGoalComparison.AtMost,
                [EChallengeType.DamageDealt] = EChallengeGoalComparison.AtLeast,
                [EChallengeType.BattlesWon] = EChallengeGoalComparison.AtLeast,
                [EChallengeType.SkillsUsed] = EChallengeGoalComparison.AtLeast,
            };

            // A newly-added challenge type must declare its intended direction above before this passes.
            Assert.Equal(Enum.GetValues<EChallengeType>().ToHashSet(), expected.Keys.ToHashSet());

            foreach (var (type, comparison) in expected)
            {
                Assert.Equal(comparison, new ChallengeType(type).GoalComparison);
            }
        }

        [Fact]
        public void Name_IsHumanReadableWithSpaces()
        {
            var challengeType = new ChallengeType(EChallengeType.EnemiesKilled);

            Assert.Equal("Enemies Killed", challengeType.Name);
        }

        [Fact]
        public void EveryChallengeTypeIsConstructible()
        {
            // Guards against an EChallengeType value that the constructor cannot handle.
            foreach (var type in Enum.GetValues<EChallengeType>())
            {
                var challengeType = new ChallengeType(type);
                Assert.Equal(type, challengeType.Id);
            }
        }

        [Fact]
        public void EveryChallengeTypeEitherMapsToAStatisticOrIsExplicitlyHandled()
        {
            // The type→statistic resolution is total and fail-loud: every challenge type must either
            // carry a backing statistic or be a deliberately statistic-less accumulator (LevelReached).
            // A newly-added type with no GetStatisticType arm throws in the constructor rather than
            // silently resolving to "no statistic" and never progressing.
            var statisticLess = new HashSet<EChallengeType> { EChallengeType.LevelReached };

            foreach (var type in Enum.GetValues<EChallengeType>())
            {
                var challengeType = new ChallengeType(type);
                if (statisticLess.Contains(type))
                {
                    Assert.Null(challengeType.StatisticType);
                }
                else
                {
                    Assert.NotNull(challengeType.StatisticType);
                }
            }
        }
    }
}
