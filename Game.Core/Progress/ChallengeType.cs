namespace Game.Core.Progress
{
    public class ChallengeType
    {
        public EChallengeType Id { get; }
        public StatisticType? StatisticType { get; }
        public EChallengeGoalComparison GoalComparison { get; }
        public string Name { get; }

        public ChallengeType(EChallengeType id)
        {
            Id = id;
            Name = id.ToString().SpaceWords();

            var statisticType = GetStatisticType(id);
            if (statisticType.HasValue)
            {
                StatisticType = new StatisticType(statisticType.Value);
            }

            GoalComparison = GetGoalComparison(StatisticType);
        }

        public static IEnumerable<ChallengeType> GetAll() => Enum.GetValues<EChallengeType>().Select(id => new ChallengeType(id));

        private static EChallengeGoalComparison GetGoalComparison(StatisticType? statisticType)
        {
            // The comparison direction follows the backing statistic's aggregation: a minimized statistic
            // (lower is better, e.g. FastestVictory) completes "at or below" the goal, everything else "at
            // least". A challenge with no backing statistic (e.g. LevelReached) accumulates, so AtLeast.
            return statisticType?.AggregationKind == EAggregationKind.Min
                ? EChallengeGoalComparison.AtMost
                : EChallengeGoalComparison.AtLeast;
        }

        private static EStatisticType? GetStatisticType(EChallengeType id)
        {
            return id switch
            {
                EChallengeType.EnemiesKilled => EStatisticType.EnemiesKilled,
                EChallengeType.BossesDefeated => EStatisticType.BossesDefeated,
                EChallengeType.ZonesCleared => EStatisticType.ZonesCleared,
                EChallengeType.TimeTrial => EStatisticType.FastestVictory,
                EChallengeType.DamageDealt => EStatisticType.DamageDealt,
                EChallengeType.BattlesWon => EStatisticType.BattlesWon,
                EChallengeType.SkillsUsed => EStatisticType.SkillsUsed,
                EChallengeType.KillsByDamageType => EStatisticType.KillsByDamageType,
                // LevelReached accumulates the player's level directly and has no backing statistic;
                // UpdateChallengeProgress special-cases it. Listed explicitly so the default arm can
                // fail loud on a newly-added type that was wired up only half-way.
                EChallengeType.LevelReached => null,
                _ => throw new ArgumentOutOfRangeException(nameof(id), id,
                    $"No statistic mapping defined for challenge type {id}.")
            };
        }
    }
}
