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
            GoalComparison = GetGoalComparison(id);

            var statisticType = GetStatisticType(id);
            if (statisticType.HasValue)
            {
                StatisticType = new StatisticType(statisticType.Value);
            }
        }

        public static IEnumerable<ChallengeType> GetAll() => Enum.GetValues<EChallengeType>().Select(id => new ChallengeType(id));

        private static EChallengeGoalComparison GetGoalComparison(EChallengeType id)
        {
            return id switch
            {
                // Time trials track the fastest victory time, where a lower value is better.
                EChallengeType.TimeTrial => EChallengeGoalComparison.AtMost,
                _ => EChallengeGoalComparison.AtLeast,
            };
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
                _ => null
            };
        }
    }
}
