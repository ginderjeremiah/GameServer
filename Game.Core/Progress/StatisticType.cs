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

        /// <summary>
        /// How this statistic's value is aggregated across the battles that report it. A derived domain
        /// fact, so a single source of truth drives both the recording mutator
        /// (<see cref="PlayerProgress.RecordBattleCompleted"/>) and the goal comparison of a challenge that
        /// tracks this statistic (<see cref="ChallengeType.GoalComparison"/>) rather than each hard-coding
        /// the direction independently.
        /// </summary>
        public EAggregationKind AggregationKind { get; }
        public string Name { get; }

        public StatisticType(EStatisticType id)
        {
            Id = id;
            EntityType = GetEntityType(id);
            BossOnly = GetBossOnly(id);
            AggregationKind = GetAggregationKind(id);
            Name = id.ToString().SpaceWords();
        }

        public static IEnumerable<StatisticType> GetAll() => Enum.GetValues<EStatisticType>().Select(id => new StatisticType(id));

        /// <summary>
        /// The aggregation direction for a statistic type, exposed statically so the per-battle recording
        /// path can dispatch on it without allocating a full <see cref="StatisticType"/> per write.
        /// </summary>
        public static EAggregationKind GetAggregationKind(EStatisticType id)
        {
            return id switch
            {
                // FastestVictory keeps the lowest victory time — lower is better, so a TimeTrial challenge
                // backed by it completes on an "at most" comparison.
                FastestVictory => EAggregationKind.Min,
                // HighestSingleAttackDamage keeps the single largest hit ever landed.
                HighestSingleAttackDamage => EAggregationKind.Max,
                // ZonesCleared's per-zone row is a binary "cleared" flag and the global counter a distinct
                // count; both only ever move upward, so they sum like the rest of the accumulating stats.
                _ => EAggregationKind.Sum,
            };
        }

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
                KillsByDamageType => DamageType,
                _ => None
            };
        }
    }
}
