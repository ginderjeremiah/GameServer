using Game.Core.Progress;

namespace Game.Core.Challenges
{
    public class ChallengeType
    {
        public EChallengeType Id { get; }
        public StatisticType? StatisticType { get; }
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
