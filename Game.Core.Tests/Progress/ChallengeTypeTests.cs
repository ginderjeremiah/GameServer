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
    }
}
