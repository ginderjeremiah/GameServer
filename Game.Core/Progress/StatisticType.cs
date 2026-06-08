using static Game.Core.EEntityType;
using static Game.Core.EStatisticType;

namespace Game.Core.Progress
{
    public class StatisticType
    {
        public EStatisticType Id { get; }
        public EEntityType EntityType { get; }

        /// <summary>
        /// Whether this statistic is only ever recorded for boss enemies, so its
        /// per-enemy breakdown (and any challenge that targets a specific enemy) is
        /// restricted to bosses. A domain fact, derived alongside <see cref="EntityType"/>,
        /// so a single source of truth drives both the statistics display and the admin
        /// challenge editor's target-entity picker rather than each special-casing the type.
        /// </summary>
        public bool BossOnly { get; }
        public string Name { get; }

        public StatisticType(EStatisticType id)
        {
            Id = id;
            EntityType = GetEntityType(id);
            BossOnly = GetBossOnly(id);
            Name = id.ToString().SpaceWords();
        }

        public static IEnumerable<StatisticType> GetAll() => Enum.GetValues<EStatisticType>().Select(id => new StatisticType(id));

        private static bool GetBossOnly(EStatisticType id)
        {
            return id switch
            {
                // BossesDefeated is only ever incremented on a dedicated-boss victory
                // (PlayerProgress.RecordBattleCompleted), so both its per-enemy breakdown
                // and a challenge that targets a specific enemy are restricted to bosses.
                BossesDefeated => true,
                _ => false
            };
        }

        private static EEntityType GetEntityType(EStatisticType id)
        {
            return id switch
            {
                EnemiesKilled => Enemy,
                // BossesDefeated is recorded per-boss (and as a global total) in
                // PlayerProgress.RecordBattleCompleted, so its declared breakdown is by enemy.
                BossesDefeated => Enemy,
                ZonesCleared => Zone,
                DamageDealt => Skill,
                HighestSingleAttackDamage => Skill,
                EnemiesEncountered => Enemy,
                BattlesWon => Enemy,
                BattlesLost => Enemy,
                BattlesAbandoned => Enemy,
                // FastestVictory is recorded per enemy (and as a global min) in
                // PlayerProgress.RecordBattleCompleted, so its declared breakdown is by enemy.
                FastestVictory => Enemy,
                SkillsUsed => Skill,
                _ => None
            };
        }
    }
}
