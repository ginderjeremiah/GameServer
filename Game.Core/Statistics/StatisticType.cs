using static Game.Core.EEntityType;
using static Game.Core.EStatisticType;

namespace Game.Core.Statistics
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

        private static EEntityType GetEntityType(EStatisticType id)
        {
            return id switch
            {
                EnemiesKilled => Enemy,
                ZonesCleared => Zone,
                EnemiesEncountered => Enemy,
                BattlesWon => Enemy,
                BattlesLost => Enemy,
                TotalSkillsUsed => Skill,
                _ => None
            };
        }
    }
}
