using Game.Core.Battle;
using Game.Core.Enemies;
using Game.Core.Players;

namespace Game.Core.Progress
{
    public class PlayerProgress
    {
        private readonly Dictionary<(EStatisticType Type, int? EntityId), PlayerStatistic> _statistics;
        private readonly Dictionary<int, PlayerChallenge> _challenges;

        public Player Player { get; }
        public IEnumerable<PlayerStatistic> Statistics => _statistics.Values;
        public IEnumerable<PlayerChallenge> ChallengeProgress => _challenges.Values;

        public PlayerProgress(
            Player player,
            IEnumerable<PlayerStatistic> statistics,
            IEnumerable<PlayerChallenge> challengeProgress)
        {
            Player = player;
            _statistics = statistics.ToDictionary(s => (s.Type, s.EntityId));
            _challenges = challengeProgress.ToDictionary(c => c.Challenge.Id);
        }

        public void RecordBattleCompleted(
            Enemy enemy, bool victory, bool playerDied, int totalMs, BattleStats stats, bool isBossBattle, int zoneId)
        {
            Increment(EStatisticType.DamageDealt, null, (decimal)Math.Round(stats.PlayerDamageDealt, 3));
            SetMax(EStatisticType.HighestSingleAttackDamage, null, (decimal)Math.Round(stats.HighestPlayerAttack, 3));
            Increment(EStatisticType.DamageTaken, null, (decimal)Math.Round(stats.PlayerDamageTaken, 3));
            Increment(EStatisticType.EnemiesEncountered, null, 1);
            Increment(EStatisticType.EnemiesEncountered, enemy.Id, 1);

            if (victory)
            {
                Increment(EStatisticType.BattlesWon, null, 1);
                Increment(EStatisticType.BattlesWon, enemy.Id, 1);
                SetMin(EStatisticType.FastestVictory, null, totalMs / 1000m);
                SetMin(EStatisticType.FastestVictory, enemy.Id, totalMs / 1000m);
                Increment(EStatisticType.EnemiesKilled, null, 1);
                Increment(EStatisticType.EnemiesKilled, enemy.Id, 1);

                // A dedicated-boss victory both farms the boss and (on the first clear) clears its zone. The
                // boss-ness comes from the explicit challenge marker (threaded from PlayerState), not the
                // enemy's IsBoss flag, so only the "Challenge Boss" path counts — never a boss that happens to
                // roll out of a random spawn table.
                if (isBossBattle)
                {
                    // BossesDefeated is the farm counter: it increments on every dedicated-boss victory,
                    // tracked globally and per-boss so challenges can target either "defeat any boss" or a
                    // specific boss repeatedly.
                    Increment(EStatisticType.BossesDefeated, null, 1);
                    Increment(EStatisticType.BossesDefeated, enemy.Id, 1);

                    // ZonesCleared counts distinct zones ever cleared, not boss-victory events. The per-zone
                    // entry is a binary "cleared" flag, and the global counter only bumps on a zone's first
                    // clear (its 0 -> 1 transition), so re-farming a boss never re-counts the zone.
                    if (GetStatisticValue(EStatisticType.ZonesCleared, zoneId) == 0)
                    {
                        Increment(EStatisticType.ZonesCleared, null, 1);
                        Increment(EStatisticType.ZonesCleared, zoneId, 1);
                    }
                }
            }
            else if (playerDied)
            {
                // The player died, so the battle was genuinely lost.
                Increment(EStatisticType.BattlesLost, null, 1);
                Increment(EStatisticType.BattlesLost, enemy.Id, 1);
            }
            else
            {
                // Neither combatant died — the battle was abandoned mid-fight (e.g. the player retreated
                // from a boss or switched zones). Tracked separately from a loss so the two are distinct
                // numbers (#202).
                Increment(EStatisticType.BattlesAbandoned, null, 1);
                Increment(EStatisticType.BattlesAbandoned, enemy.Id, 1);
            }

            if (playerDied)
            {
                Increment(EStatisticType.PlayerDeaths, null, 1);
            }

            Increment(EStatisticType.TotalBattleTime, null, totalMs / 1000m);
            Increment(EStatisticType.SkillsUsed, null, stats.PlayerSkillsUsed);

            foreach (var (skillId, skillStats) in stats.SkillStats)
            {
                Increment(EStatisticType.SkillsUsed, skillId, skillStats.Uses);
                Increment(EStatisticType.DamageDealt, skillId, (decimal)Math.Round(skillStats.TotalDamage, 3));
                SetMax(EStatisticType.HighestSingleAttackDamage, skillId, (decimal)Math.Round(skillStats.HighestSingleAttack, 3));
            }
        }

        public List<CompletedChallenge> EvaluateChallenges(IReadOnlyList<Challenge> allChallenges)
        {
            var completed = new List<CompletedChallenge>();

            foreach (var challenge in allChallenges)
            {
                if (_challenges.TryGetValue(challenge.Id, out var playerChallenge) && playerChallenge.Completed)
                {
                    continue;
                }

                if (playerChallenge is null)
                {
                    playerChallenge = new PlayerChallenge(challenge, progress: 0, completed: false);
                    _challenges[challenge.Id] = playerChallenge;
                }

                challenge.UpdateChallengeProgress(playerChallenge, this);
                if (playerChallenge.Completed)
                {
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

        public decimal GetStatisticValue(EStatisticType type, int? entityId)
        {
            var key = (type, entityId);
            return _statistics.TryGetValue(key, out var stat) ? stat.Value : 0m;
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
