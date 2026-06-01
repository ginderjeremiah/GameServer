using Game.Abstractions.DataAccess;
using Game.Core;
using Game.Core.Statistics;
using Game.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using CoreChallenge = Game.Core.Challenges.PlayerChallenge;
using CoreStat = Game.Core.Statistics.PlayerStatistic;
using EntityChallenge = Game.Abstractions.Entities.PlayerChallenge;
using EntityStat = Game.Abstractions.Entities.PlayerStatistic;

namespace Game.DataAccess.Repositories
{
    internal class PlayerProgressRepository(GameContext context) : IPlayerProgressRepository
    {
        private readonly GameContext _context = context;
        private Dictionary<(int StatTypeId, int? EntityId), EntityStat>? _loadedStats;
        private Dictionary<int, EntityChallenge>? _loadedChallenges;

        public async Task<PlayerProgress> Load(int playerId)
        {
            var statEntities = await _context.PlayerStatistics
                .Where(ps => ps.PlayerId == playerId)
                .ToListAsync();

            var challengeEntities = await _context.PlayerChallenges
                .Where(pc => pc.PlayerId == playerId)
                .ToListAsync();

            _loadedStats = statEntities.ToDictionary(
                e => (e.StatisticTypeId, e.EntityId));
            _loadedChallenges = challengeEntities.ToDictionary(
                e => e.ChallengeId);

            var coreStats = statEntities.Select(e => new CoreStat
            {
                Type = (EStatisticType)e.StatisticTypeId,
                EntityId = e.EntityId,
                Value = e.Value,
            });

            var coreChallenges = challengeEntities.Select(e => new CoreChallenge
            {
                ChallengeId = e.ChallengeId,
                Progress = e.Progress,
                ProgressGoal = 0,
                Completed = e.Completed,
                CompletedAt = e.CompletedAt,
            });

            return new PlayerProgress(playerId, coreStats, coreChallenges);
        }

        public void Save(PlayerProgress progress)
        {
            foreach (var stat in progress.Statistics)
            {
                var key = ((int)stat.Type, stat.EntityId);
                if (_loadedStats!.TryGetValue(key, out var entity))
                {
                    entity.Value = stat.Value;
                }
                else
                {
                    _context.PlayerStatistics.Add(new EntityStat
                    {
                        PlayerId = progress.PlayerId,
                        StatisticTypeId = (int)stat.Type,
                        EntityId = stat.EntityId,
                        Value = stat.Value,
                    });
                }
            }

            foreach (var cp in progress.ChallengeProgress)
            {
                if (_loadedChallenges!.TryGetValue(cp.ChallengeId, out var entity))
                {
                    entity.Progress = cp.Progress;
                    entity.Completed = cp.Completed;
                    entity.CompletedAt = cp.CompletedAt;
                }
                else
                {
                    _context.PlayerChallenges.Add(new EntityChallenge
                    {
                        PlayerId = progress.PlayerId,
                        ChallengeId = cp.ChallengeId,
                        Progress = cp.Progress,
                        Completed = cp.Completed,
                        CompletedAt = cp.CompletedAt,
                    });
                }
            }
        }
    }
}
