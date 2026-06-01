using Game.Core;
using Game.Core.Progress;
using Xunit;

namespace Game.Core.Tests.Progress
{
    public class StatisticTypeTests
    {
        [Theory]
        [InlineData(EStatisticType.EnemiesKilled, EEntityType.Enemy)]
        [InlineData(EStatisticType.EnemiesEncountered, EEntityType.Enemy)]
        [InlineData(EStatisticType.BattlesWon, EEntityType.Enemy)]
        [InlineData(EStatisticType.BattlesLost, EEntityType.Enemy)]
        [InlineData(EStatisticType.ZonesCleared, EEntityType.Zone)]
        [InlineData(EStatisticType.DamageDealt, EEntityType.Skill)]
        [InlineData(EStatisticType.HighestSingleAttackDamage, EEntityType.Skill)]
        [InlineData(EStatisticType.SkillsUsed, EEntityType.Skill)]
        public void EntityScopedStatistics_MapToExpectedEntityType(EStatisticType type, EEntityType expected)
        {
            var statisticType = new StatisticType(type);

            Assert.Equal(type, statisticType.Id);
            Assert.Equal(expected, statisticType.EntityType);
        }

        [Theory]
        [InlineData(EStatisticType.BossesDefeated)]
        [InlineData(EStatisticType.DamageTaken)]
        [InlineData(EStatisticType.DamageHealed)]
        [InlineData(EStatisticType.PlayerDeaths)]
        [InlineData(EStatisticType.TotalBattleTime)]
        [InlineData(EStatisticType.FastestVictory)]
        public void GlobalStatistics_HaveNoEntityType(EStatisticType type)
        {
            var statisticType = new StatisticType(type);

            Assert.Equal(EEntityType.None, statisticType.EntityType);
        }

        [Fact]
        public void Name_IsHumanReadableWithSpaces()
        {
            var statisticType = new StatisticType(EStatisticType.HighestSingleAttackDamage);

            Assert.Equal("Highest Single Attack Damage", statisticType.Name);
        }

        [Fact]
        public void GetAll_ReturnsOneEntryPerStatisticType()
        {
            var all = StatisticType.GetAll().ToList();

            var expectedIds = Enum.GetValues<EStatisticType>();
            Assert.Equal(expectedIds.Length, all.Count);
            Assert.Equal(expectedIds, all.Select(s => s.Id).ToArray());
        }
    }
}
