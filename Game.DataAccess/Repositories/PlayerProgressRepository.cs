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
            var loadedStats = await GetLoadedStats(player.Id);
            var loadedChallenges = await GetLoadedChallenges(player.Id);

            var coreStats = loadedStats.Values.Select(PlayerProgressMapper.ToCore);

            var coreChallenges = loadedChallenges.Values
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

        public async Task<HashSet<int>> GetCompletedChallengeIds(int playerId)
        {
            var ids = await _context.PlayerChallenges
                .AsNoTracking()
                .Where(pc => pc.PlayerId == playerId && pc.Completed)
                .Select(pc => pc.ChallengeId)
                .ToListAsync();

            return [.. ids];
        }

        public async Task Save(PlayerProgress progress)
        {
            var loadedStats = await GetLoadedStats(progress.Player.Id);
            var loadedChallenges = await GetLoadedChallenges(progress.Player.Id);

            foreach (var stat in progress.Statistics)
            {
                var key = ((int)stat.Type, stat.EntityId);
                if (loadedStats.TryGetValue(key, out var entity))
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
                if (loadedChallenges.TryGetValue(cp.Challenge.Id, out var entity))
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

        /// <summary>
        /// Lazily loads (and caches for the lifetime of this scoped repository) the player's existing
        /// statistic rows as tracked entities, keyed by their statistic-type/entity pair. Shared by
        /// <see cref="Load"/> and <see cref="Save"/> so that <see cref="Save"/> no longer depends on
        /// <see cref="Load"/> having run first: it fetches the snapshot on demand when it isn't already
        /// cached. The common load-then-save flow reuses the cached snapshot, so no redundant query is
        /// issued.
        /// </summary>
        private async Task<Dictionary<(int StatTypeId, int? EntityId), EntityStat>> GetLoadedStats(int playerId)
        {
            return _loadedStats ??= (await _context.PlayerStatistics
                    .Where(ps => ps.PlayerId == playerId)
                    .ToListAsync())
                .ToDictionary(e => (e.StatisticTypeId, e.EntityId));
        }

        /// <summary>
        /// Lazily loads (and caches for the lifetime of this scoped repository) the player's existing
        /// challenge-progress rows as tracked entities, keyed by challenge id. See
        /// <see cref="GetLoadedStats"/> for the rationale behind the on-demand load.
        /// </summary>
        private async Task<Dictionary<int, EntityChallenge>> GetLoadedChallenges(int playerId)
        {
            return _loadedChallenges ??= (await _context.PlayerChallenges
                    .Where(pc => pc.PlayerId == playerId)
                    .ToListAsync())
                .ToDictionary(e => e.ChallengeId);
        }
    }
}
