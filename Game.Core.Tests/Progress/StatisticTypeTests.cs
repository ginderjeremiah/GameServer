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

        [Theory]
        // Min-aggregated: only the lowest reported value is kept (lower is better).
        [InlineData(EStatisticType.FastestVictory, EAggregationKind.Min)]
        // Max-aggregated: only the highest reported value is kept.
        [InlineData(EStatisticType.HighestSingleAttackDamage, EAggregationKind.Max)]
        // Sum-aggregated: accumulating counters/totals (a representative sample of the rest).
        [InlineData(EStatisticType.EnemiesKilled, EAggregationKind.Sum)]
        [InlineData(EStatisticType.DamageDealt, EAggregationKind.Sum)]
        [InlineData(EStatisticType.ZonesCleared, EAggregationKind.Sum)]
        [InlineData(EStatisticType.TotalBattleTime, EAggregationKind.Sum)]
        public void Statistics_MapToExpectedAggregationKind(EStatisticType type, EAggregationKind expected)
        {
            var statisticType = new StatisticType(type);

            Assert.Equal(expected, statisticType.AggregationKind);
            // The static accessor (used by the per-battle recording path) agrees with the instance property.
            Assert.Equal(expected, StatisticType.GetAggregationKind(type));
        }

        [Fact]
        public void FastestVictory_IsTheOnlyMinAggregatedStatistic()
        {
            // FastestVictory is the sole "lower is better" statistic. Asserted across every type so a
            // future minimized statistic must update this expectation (and its challenge comparison)
            // deliberately. Asserted across the whole set so the min set stays a single source of truth.
            foreach (var statisticType in StatisticType.GetAll())
            {
                var isMin = statisticType.AggregationKind == EAggregationKind.Min;
                Assert.Equal(statisticType.Id == EStatisticType.FastestVictory, isMin);
            }
        }

        [Fact]
        public void HighestSingleAttackDamage_IsTheOnlyMaxAggregatedStatistic()
        {
            foreach (var statisticType in StatisticType.GetAll())
            {
                var isMax = statisticType.AggregationKind == EAggregationKind.Max;
                Assert.Equal(statisticType.Id == EStatisticType.HighestSingleAttackDamage, isMax);
            }
        }

        [Fact]
        public void EveryStatisticHasADefinedAggregationKind()
        {
            // Guards against an EStatisticType the aggregation mapping silently falls through on.
            foreach (var type in Enum.GetValues<EStatisticType>())
            {
                Assert.Contains(StatisticType.GetAggregationKind(type),
                    new[] { EAggregationKind.Sum, EAggregationKind.Max, EAggregationKind.Min });
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
