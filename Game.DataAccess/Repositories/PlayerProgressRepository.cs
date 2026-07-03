using Game.Abstractions.Auth;
using Game.Abstractions.DataAccess;
using Game.Abstractions.Infrastructure;
using Game.Core;
using Game.Core.Events;
using Game.Core.Players;
using Game.Core.Progress;
using Game.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using CoreChallenge = Game.Core.Progress.PlayerChallenge;
using CoreProficiency = Game.Core.Progress.PlayerProficiency;
using CoreStat = Game.Core.Progress.PlayerStatistic;

namespace Game.DataAccess.Repositories
{
    internal class PlayerProgressRepository(
        GameContext context,
        IChallenges challenges,
        ICacheService cache,
        IPubSubService pubsub,
        PlayerUpdateBatch updateBatch) : IPlayerProgressRepository
    {
        private readonly GameContext _context = context;
        private readonly IChallenges _challenges = challenges;
        private readonly ICacheService _cache = cache;
        private readonly IPubSubService _pubsub = pubsub;
        private readonly PlayerUpdateBatch _updateBatch = updateBatch;

        // Sliding idle TTL for the cached progress aggregate, mirroring the player cache (#439): written on
        // every save and load-miss re-cache, refreshed on every hit, so an active player never ages out while
        // a dormant one does. It dwarfs the sub-second write-behind drain window, so a key never expires
        // mid-drain (see docs/backend-persistence.md -> Caching and Pub/Sub). It shares the same anchor as the player and
        // session caches (AuthConstants.RefreshTokenLifetime) so a retune of that budget keeps them aligned.
        private static readonly TimeSpan ProgressCacheTtl = AuthConstants.RefreshTokenLifetime;

        private static string ProgressKey(int playerId) => $"{Constants.CACHE_PROGRESS_PREFIX}_{playerId}";

        public async Task<PlayerProgress> Load(Player player, CancellationToken cancellationToken = default)
        {
            var cached = await GetCachedProgress(player.Id, cancellationToken);
            return new PlayerProgress(
                player,
                cached.Statistics.Select(ToCoreStatistic),
                cached.Challenges.Select(ToCoreChallenge),
                cached.Proficiencies.Select(ToCoreProficiency));
        }

        public async Task<List<CoreStat>> GetStatistics(int playerId, CancellationToken cancellationToken = default)
        {
            var cached = await GetCachedProgress(playerId, cancellationToken);
            return cached.Statistics.Select(ToCoreStatistic).ToList();
        }

        public async Task<List<CoreChallenge>> GetChallenges(int playerId, CancellationToken cancellationToken = default)
        {
            var cached = await GetCachedProgress(playerId, cancellationToken);
            return cached.Challenges.Select(ToCoreChallenge).ToList();
        }

        public async Task<HashSet<int>> GetCompletedChallengeIds(int playerId, CancellationToken cancellationToken = default)
        {
            var cached = await GetCachedProgress(playerId, cancellationToken);
            return [.. cached.Challenges.Where(c => c.Completed).Select(c => c.ChallengeId)];
        }

        public async Task<List<CoreProficiency>> GetProficiencies(int playerId, CancellationToken cancellationToken = default)
        {
            var cached = await GetCachedProgress(playerId, cancellationToken);
            return cached.Proficiencies.Select(ToCoreProficiency).ToList();
        }

        public async Task Save(PlayerProgress progress, CancellationToken cancellationToken = default)
        {
            // Nothing mutated since load -> the cache already holds the current snapshot (and reads slide its
            // TTL), so there is nothing to persist and no reason to rewrite the cache.
            var changed = ToCached(progress.DirtyStatistics, progress.DirtyChallenges, progress.DirtyProficiencies);
            if (changed.Statistics.Count == 0 && changed.Challenges.Count == 0 && changed.Proficiencies.Count == 0)
            {
                return;
            }

            var playerId = progress.Player.Id;

            // Enqueue the durable write-behind event first, then advance the cache. If the enqueue throws, the
            // cache must not have moved on to a snapshot that was never enqueued (and never will be), which
            // would be a silently lost write once the cache later evicts. Persist only the rows that changed
            // this save, as one event; the consumer upserts them to their absolute values off the response path.
            var envelope = new DomainEventEnvelope
            {
                Type = nameof(ProgressUpdatedEvent),
                Payload = new ProgressUpdatedEvent
                {
                    PlayerId = playerId,
                    Statistics = changed.Statistics,
                    Challenges = changed.Challenges,
                    Proficiencies = changed.Proficiencies,
                }.Serialize(),
            };
            _updateBatch.Add(envelope);

            // The cache is the source of truth, so the advance writes the full current snapshot (absolute
            // values). Capture it now (off the live progress aggregate) so a deferred advance still snapshots
            // this save's state, not a later mutation's.
            var snapshot = ToCached(progress.Statistics, progress.ChallengeProgress, progress.Proficiencies);
            void AdvanceCache() => _cache.SetAndForget(ProgressKey(playerId), snapshot, ProgressCacheTtl);

            if (_updateBatch.PlayerSaveInProgress)
            {
                // Riding the in-flight player save's single flush (the live battle-completion hot path): the
                // event is already buffered above; defer the cache advance so SavePlayer runs it only after
                // that flush enqueues the event, collapsing both writes onto one queue round-trip (#1237).
                _updateBatch.OnFlushed(AdvanceCache);
            }
            else
            {
                // Standalone progress save (e.g. the offline-rewards batch): flush our own event, then advance
                // the cache — preserving publish-before-cache so the write is never stranded. FlushAsync leaves
                // the event buffered for the next flush attempt if the publish itself fails, rather than losing
                // it (#1494).
                await _updateBatch.FlushAsync(_pubsub, cancellationToken);
                AdvanceCache();
            }
        }

        private async Task<CachedPlayerProgress> GetCachedProgress(int playerId, CancellationToken cancellationToken)
        {
            var key = ProgressKey(playerId);
            var cached = await _cache.Get<CachedPlayerProgress>(key, cancellationToken);
            if (cached is null)
            {
                cached = await LoadFromDb(playerId, cancellationToken);
                _cache.SetAndForget(key, cached, ProgressCacheTtl);
            }
            else
            {
                // Sliding expiration: a cache hit refreshes the idle TTL so an active player never ages out.
                _cache.ExpireAndForget(key, ProgressCacheTtl);
            }

            return cached;
        }

        private async Task<CachedPlayerProgress> LoadFromDb(int playerId, CancellationToken cancellationToken)
        {
            var statistics = await _context.PlayerStatistics
                .AsNoTracking()
                .Where(ps => ps.PlayerId == playerId)
                .Select(ps => new CachedPlayerStatistic
                {
                    StatisticTypeId = ps.StatisticTypeId,
                    EntityId = ps.EntityId,
                    Value = ps.Value,
                })
                .ToListAsync(cancellationToken);

            var challenges = await _context.PlayerChallenges
                .AsNoTracking()
                .Where(pc => pc.PlayerId == playerId)
                .Select(pc => new CachedPlayerChallenge
                {
                    ChallengeId = pc.ChallengeId,
                    Progress = pc.Progress,
                    Completed = pc.Completed,
                    CompletedAt = pc.CompletedAt,
                })
                .ToListAsync(cancellationToken);

            var proficiencies = await _context.PlayerProficiencies
                .AsNoTracking()
                .Where(pp => pp.PlayerId == playerId)
                .Select(pp => new CachedPlayerProficiency
                {
                    ProficiencyId = pp.ProficiencyId,
                    Level = pp.Level,
                    Xp = pp.Xp,
                })
                .ToListAsync(cancellationToken);

            return new CachedPlayerProgress { Statistics = statistics, Challenges = challenges, Proficiencies = proficiencies };
        }

        private static CoreStat ToCoreStatistic(CachedPlayerStatistic cached) => new()
        {
            Type = (EStatisticType)cached.StatisticTypeId,
            EntityId = cached.EntityId,
            Value = cached.Value,
        };

        private CoreChallenge ToCoreChallenge(CachedPlayerChallenge cached) =>
            new(_challenges.GetChallenge(cached.ChallengeId), cached.Progress, cached.Completed, cached.CompletedAt);

        private static CoreProficiency ToCoreProficiency(CachedPlayerProficiency cached) => new()
        {
            ProficiencyId = cached.ProficiencyId,
            Level = cached.Level,
            Xp = cached.Xp,
        };

        private static CachedPlayerProgress ToCached(
            IEnumerable<CoreStat> statistics,
            IEnumerable<CoreChallenge> challenges,
            IEnumerable<CoreProficiency> proficiencies) => new()
            {
                Statistics = statistics.Select(s => new CachedPlayerStatistic
                {
                    StatisticTypeId = (int)s.Type,
                    EntityId = s.EntityId,
                    Value = s.Value,
                }).ToList(),
                Challenges = challenges.Select(c => new CachedPlayerChallenge
                {
                    ChallengeId = c.Challenge.Id,
                    Progress = c.Progress,
                    Completed = c.Completed,
                    CompletedAt = c.CompletedAt,
                }).ToList(),
                Proficiencies = proficiencies.Select(p => new CachedPlayerProficiency
                {
                    ProficiencyId = p.ProficiencyId,
                    Level = p.Level,
                    Xp = p.Xp,
                }).ToList(),
            };
    }
}
