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

        public void RecordBattleCompleted(int enemyId, bool victory, bool playerDied, int totalMs, BattleStats stats)
        {
            Increment(EStatisticType.TotalDamageDealt, null, (decimal)Math.Round(stats.PlayerDamageDealt, 3));
            SetMax(EStatisticType.HighestSingleAttackDamage, null, (decimal)Math.Round(stats.HighestPlayerAttack, 3));
            Increment(EStatisticType.TotalDamageTaken, null, (decimal)Math.Round(stats.PlayerDamageTaken, 3));
            Increment(EStatisticType.EnemiesEncountered, null, 1);
            Increment(EStatisticType.EnemiesEncountered, enemyId, 1);

            if (victory)
            {
                Increment(EStatisticType.BattlesWon, null, 1);
                Increment(EStatisticType.BattlesWon, enemyId, 1);
                SetMin(EStatisticType.FastestVictoryMs, null, totalMs);
                SetMin(EStatisticType.FastestVictoryMs, enemyId, totalMs);
            }
            else
            {
                Increment(EStatisticType.BattlesLost, null, 1);
                Increment(EStatisticType.BattlesLost, enemyId, 1);
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

        //TODO: Refactor this to move more logic into Challenge entity.
        public List<CompletedChallenge> EvaluateChallenges(IReadOnlyList<Challenge> allChallenges)
        {
            var completed = new List<CompletedChallenge>();

            foreach (var challenge in allChallenges)
            {
                if (_challenges.TryGetValue(challenge.Id, out var progress) && progress.Completed)
                {
                    continue;
                }

                if (challenge.StatisticType is null)
                {
                    continue;
                }

                var currentValue = GetStatistic(challenge.StatisticType.Value, challenge.TargetEntityId);
                var newProgress = Math.Min(currentValue, challenge.ProgressGoal);

                if (progress is null)
                {
                    progress = new PlayerChallenge
                    {
                        ChallengeId = challenge.Id,
                        Progress = newProgress,
                        ProgressGoal = challenge.ProgressGoal,
                        Completed = false,
                    };
                    _challenges[challenge.Id] = progress;
                }
                else
                {
                    progress.Progress = newProgress;
                }

                if (currentValue >= challenge.ProgressGoal)
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

        private decimal GetStatistic(EStatisticType type, int? entityId = null)
        {
            return _statistics.TryGetValue((type, entityId), out var stat) ? stat.Value : 0m;
        }

        private decimal Increment(EStatisticType type, int? entityId, decimal amount)
        {
            var stat = GetOrCreate(type, entityId);
            stat.Value += amount;
            return stat.Value;
        }

        private decimal SetMax(EStatisticType type, int? entityId, decimal value)
        {
            var stat = GetOrCreate(type, entityId);
            if (value > stat.Value)
            {
                stat.Value = value;
            }

            return stat.Value;
        }

        private decimal SetMin(EStatisticType type, int? entityId, decimal value)
        {
            var stat = GetOrCreate(type, entityId);
            if (stat.Value == 0 || value < stat.Value)
            {
                stat.Value = value;
            }

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
    }

    public class CompletedChallenge
    {
        public required int ChallengeId { get; set; }
        public int? RewardItemId { get; set; }
        public int? RewardItemModId { get; set; }
    }
}
