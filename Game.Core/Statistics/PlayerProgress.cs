using Game.Core.Battle;
using Game.Core.Challenges;

namespace Game.Core.Statistics
{
    public class PlayerProgress
    {
        private readonly Dictionary<(EStatisticType Type, int? EntityId), PlayerStatistic> _statistics;
        private readonly Dictionary<int, PlayerChallenge> _challenges;

        public int PlayerId { get; }
        public IEnumerable<PlayerStatistic> Statistics => _statistics.Values;
        public IEnumerable<PlayerChallenge> ChallengeProgress => _challenges.Values;

        public PlayerProgress(
            int playerId,
            IEnumerable<PlayerStatistic> statistics,
            IEnumerable<PlayerChallenge> challengeProgress)
        {
            PlayerId = playerId;
            _statistics = statistics.ToDictionary(s => (s.Type, s.EntityId));
            _challenges = challengeProgress.ToDictionary(c => c.ChallengeId);
        }

        public long GetStatistic(EStatisticType type, int? entityId = null)
        {
            return _statistics.TryGetValue((type, entityId), out var stat) ? stat.Value : 0;
        }

        public void RecordBattleCompleted(int enemyId, bool victory, bool playerDied, int totalMs, BattleStats stats)
        {
            Increment(EStatisticType.TotalDamageDealt, null, stats.PlayerDamageDealt);
            SetMax(EStatisticType.HighestSingleAttackDamage, null, stats.HighestPlayerAttack);
            Increment(EStatisticType.TotalDamageTaken, null, stats.PlayerDamageTaken);
            Increment(EStatisticType.EnemiesEncountered, null, 1);
            Increment(EStatisticType.EnemiesEncountered, enemyId, 1);

            if (victory)
            {
                Increment(EStatisticType.BattlesWon, null, 1);
                SetMin(EStatisticType.FastestVictoryMs, null, totalMs);
                SetMin(EStatisticType.FastestVictoryMs, enemyId, totalMs);
            }
            else
            {
                Increment(EStatisticType.BattlesLost, null, 1);
            }

            if (playerDied)
            {
                Increment(EStatisticType.PlayerDeaths, null, 1);
            }

            Increment(EStatisticType.TotalBattleTimeMs, null, totalMs);
            Increment(EStatisticType.TotalSkillsUsed, null, stats.PlayerSkillsUsed);
        }

        public void RecordEnemyDefeated(int enemyId)
        {
            Increment(EStatisticType.EnemiesKilled, null, 1);
            Increment(EStatisticType.EnemiesKilled, enemyId, 1);
        }

        public List<CompletedChallenge> EvaluateChallenges(IReadOnlyList<Challenge> allChallenges)
        {
            var completed = new List<CompletedChallenge>();

            foreach (var challenge in allChallenges)
            {
                if (_challenges.TryGetValue(challenge.Id, out var progress) && progress.Completed)
                    continue;

                var statType = MapChallengeToStatistic(challenge.Type);
                if (statType is null)
                    continue;

                var currentValue = GetStatistic(statType.Value, challenge.TargetEntityId);
                var newProgress = (int)Math.Min(currentValue, challenge.TargetCount);

                if (progress is null)
                {
                    progress = new PlayerChallenge
                    {
                        ChallengeId = challenge.Id,
                        Progress = newProgress,
                        TargetCount = challenge.TargetCount,
                        Completed = false,
                    };
                    _challenges[challenge.Id] = progress;
                }
                else
                {
                    progress.Progress = newProgress;
                }

                if (currentValue >= challenge.TargetCount)
                {
                    progress.Completed = true;
                    progress.CompletedAt = DateTime.UtcNow;

                    completed.Add(new CompletedChallenge
                    {
                        ChallengeId = challenge.Id,
                        RewardItemId = challenge.RewardItemId,
                        RewardItemModId = challenge.RewardItemModId,
                    });
                }
            }

            return completed;
        }

        private long Increment(EStatisticType type, int? entityId, long amount)
        {
            var stat = GetOrCreate(type, entityId);
            stat.Value += amount;
            return stat.Value;
        }

        private long SetMax(EStatisticType type, int? entityId, long value)
        {
            var stat = GetOrCreate(type, entityId);
            if (value > stat.Value)
                stat.Value = value;
            return stat.Value;
        }

        private long SetMin(EStatisticType type, int? entityId, long value)
        {
            var stat = GetOrCreate(type, entityId);
            if (stat.Value == 0 || value < stat.Value)
                stat.Value = value;
            return stat.Value;
        }

        private PlayerStatistic GetOrCreate(EStatisticType type, int? entityId)
        {
            var key = (type, entityId);
            if (!_statistics.TryGetValue(key, out var stat))
            {
                stat = new PlayerStatistic { Type = type, EntityId = entityId, Value = 0 };
                _statistics[key] = stat;
            }
            return stat;
        }

        private static EStatisticType? MapChallengeToStatistic(EChallengeType challengeType)
        {
            return challengeType switch
            {
                EChallengeType.KillCount => EStatisticType.EnemiesKilled,
                EChallengeType.BossDefeat => EStatisticType.BossesDefeated,
                EChallengeType.ZoneClear => EStatisticType.ZonesCleared,
                EChallengeType.DamageDealt => EStatisticType.TotalDamageDealt,
                EChallengeType.BattlesWon => EStatisticType.BattlesWon,
                EChallengeType.SkillsUsed => EStatisticType.TotalSkillsUsed,
                _ => null,
            };
        }
    }

    public class CompletedChallenge
    {
        public required int ChallengeId { get; set; }
        public int? RewardItemId { get; set; }
        public int? RewardItemModId { get; set; }
    }
}
