using Game.Abstractions.DataAccess;
using Game.Core.Players;
using Game.Core.Progress;
using Game.DataAccess.Mapping;
using Game.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using CoreChallenge = Game.Core.Progress.PlayerChallenge;
using CoreStat = Game.Core.Progress.PlayerStatistic;
using EntityChallenge = Game.Infrastructure.Entities.PlayerChallenge;
using EntityStat = Game.Infrastructure.Entities.PlayerStatistic;

namespace Game.DataAccess.Repositories
{
    internal class PlayerProgressRepository(GameContext context, IChallenges challenges) : IPlayerProgressRepository
    {
        private readonly GameContext _context = context;
        private readonly IChallenges _challenges = challenges;

        private Dictionary<(int StatTypeId, int? EntityId), EntityStat>? _loadedStats;
        private Dictionary<int, EntityChallenge>? _loadedChallenges;

        public async Task<PlayerProgress> Load(Player player)
        {
            if (_loadedStats is null)
            {
                var statEntities = await _context.PlayerStatistics
                    .Where(ps => ps.PlayerId == player.Id)
                    .ToListAsync();

                _loadedStats = statEntities.ToDictionary(
                    e => (e.StatisticTypeId, e.EntityId));
            }

            if (_loadedChallenges is null)
            {
                var challengeEntities = await _context.PlayerChallenges
                    .Where(pc => pc.PlayerId == player.Id)
                    .ToListAsync();

                _loadedChallenges = challengeEntities.ToDictionary(
                    e => e.ChallengeId);
            }

            var coreStats = _loadedStats.Values.Select(PlayerProgressMapper.ToCore);

            var coreChallenges = _loadedChallenges.Values
                .Select(e => PlayerProgressMapper.ToCore(e, _challenges.GetChallenge(e.ChallengeId)));

            return new PlayerProgress(player, coreStats, coreChallenges);
        }

        public async Task<List<CoreStat>> GetStatistics(int playerId)
        {
            var entities = await _context.PlayerStatistics
                .AsNoTracking()
                .Where(ps => ps.PlayerId == playerId)
                .ToListAsync();

            return entities.Select(PlayerProgressMapper.ToCore).ToList();
        }

        public async Task<List<CoreChallenge>> GetChallenges(int playerId)
        {
            var entities = await _context.PlayerChallenges
                .AsNoTracking()
                .Where(pc => pc.PlayerId == playerId)
                .ToListAsync();

            return entities
                .Select(e => PlayerProgressMapper.ToCore(e, _challenges.GetChallenge(e.ChallengeId)))
                .ToList();
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
                        PlayerId = progress.Player.Id,
                        StatisticTypeId = (int)stat.Type,
                        EntityId = stat.EntityId,
                        Value = stat.Value,
                    });
                }
            }

            foreach (var cp in progress.ChallengeProgress)
            {
                if (_loadedChallenges!.TryGetValue(cp.Challenge.Id, out var entity))
                {
                    entity.Progress = cp.Progress;
                    entity.Completed = cp.Completed;
                    entity.CompletedAt = cp.CompletedAt;
                }
                else
                {
                    _context.PlayerChallenges.Add(new EntityChallenge
                    {
                        PlayerId = progress.Player.Id,
                        ChallengeId = cp.Challenge.Id,
                        Progress = cp.Progress,
                        Completed = cp.Completed,
                        CompletedAt = cp.CompletedAt,
                    });
                }
            }
        }
    }
}
