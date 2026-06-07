using static Game.Core.EEntityType;
using static Game.Core.EStatisticType;

namespace Game.Core.Progress
{
    public class StatisticType
    {
        public EStatisticType Id { get; }
        public EEntityType EntityType { get; }
        public string Name { get; }

        public StatisticType(EStatisticType id)
        {
            Id = id;
            EntityType = GetEntityType(id);
            Name = id.ToString().SpaceWords();
        }

        public static IEnumerable<StatisticType> GetAll() => Enum.GetValues<EStatisticType>().Select(id => new StatisticType(id));

        private static EEntityType GetEntityType(EStatisticType id)
        {
            return id switch
            {
                EnemiesKilled => Enemy,
                ZonesCleared => Zone,
                DamageDealt => Skill,
                HighestSingleAttackDamage => Skill,
                EnemiesEncountered => Enemy,
                BattlesWon => Enemy,
                BattlesLost => Enemy,
                // FastestVictory is recorded per enemy (and as a global min) in
                // PlayerProgress.RecordBattleCompleted, so its declared breakdown is by enemy.
                FastestVictory => Enemy,
                SkillsUsed => Skill,
                _ => None
            };
        }
    }
}
