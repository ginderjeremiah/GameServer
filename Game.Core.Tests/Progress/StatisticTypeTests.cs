using Game.Core;
using Game.Core.Progress;
using Xunit;

namespace Game.Core.Tests.Progress
{
    public class StatisticTypeTests
    {
        [Theory]
        [InlineData(EStatisticType.EnemiesKilled, EEntityType.Enemy)]
        [InlineData(EStatisticType.BossesDefeated, EEntityType.Enemy)]
        [InlineData(EStatisticType.EnemiesEncountered, EEntityType.Enemy)]
        [InlineData(EStatisticType.BattlesWon, EEntityType.Enemy)]
        [InlineData(EStatisticType.BattlesLost, EEntityType.Enemy)]
        [InlineData(EStatisticType.BattlesAbandoned, EEntityType.Enemy)]
        [InlineData(EStatisticType.FastestVictory, EEntityType.Enemy)]
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
        [InlineData(EStatisticType.DamageTaken)]
        [InlineData(EStatisticType.DamageHealed)]
        [InlineData(EStatisticType.PlayerDeaths)]
        [InlineData(EStatisticType.TotalBattleTime)]
        public void GlobalStatistics_HaveNoEntityType(EStatisticType type)
        {
            var statisticType = new StatisticType(type);

            Assert.Equal(EEntityType.None, statisticType.EntityType);
        }

        [Fact]
        public void BossesDefeated_IsTheOnlyBossOnlyStatistic()
        {
            // BossesDefeated only ever increments on a dedicated-boss victory, so it is the
            // sole boss-only statistic; this single source of truth drives the admin challenge
            // editor's enemy target-picker filter. Asserted across every type so a future
            // boss-only statistic must update this expectation deliberately.
            foreach (var statisticType in StatisticType.GetAll())
            {
                Assert.Equal(statisticType.Id == EStatisticType.BossesDefeated, statisticType.BossOnly);
            }
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
