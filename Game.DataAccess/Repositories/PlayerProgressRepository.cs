using Game.Abstractions.Auth;
using Game.Abstractions.DataAccess;
using Game.Abstractions.Infrastructure;
using Game.Core;
using Game.Core.Events;
using Game.Core.Players;
using Game.Core.Progress;
using Game.DataAccess.Mapping;
using Game.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Globalization;
using System.Text.Json;
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
        PlayerUpdateBatch updateBatch,
        ILogger<PlayerProgressRepository> logger) : IPlayerProgressRepository
    {
        private readonly GameContext _context = context;
        private readonly IChallenges _challenges = challenges;
        private readonly ICacheService _cache = cache;
        private readonly IPubSubService _pubsub = pubsub;
        private readonly PlayerUpdateBatch _updateBatch = updateBatch;
        private readonly ILogger<PlayerProgressRepository> _logger = logger;

        // Sliding idle TTL for the cached progress aggregate, mirroring the player cache (#439): written on
        // every save and load-miss re-cache, refreshed on every hit, so an active player never ages out while
        // a dormant one does. It dwarfs the sub-second write-behind drain window, so a key never expires
        // mid-drain (see docs/backend-persistence.md -> Caching and Pub/Sub). It shares the same anchor as the player and
        // session caches (AuthConstants.RefreshTokenLifetime) so a retune of that budget keeps them aligned.
        private static readonly TimeSpan ProgressCacheTtl = AuthConstants.RefreshTokenLifetime;

        private static string ProgressKey(int playerId) => $"{Constants.CACHE_PROGRESS_PREFIX}_{playerId}";

        // Per-scope read memo (#1820): a socket command opens one DI scope, so this repository instance is
        // shared by every read/save within that single command. Battle-lifecycle commands read the same
        // progress hash more than once per command (e.g. the zone-unlock gate then the battle-snapshot
        // proficiency capture, or the battle-completion handler's Load then the bundled next-battle prefetch's
        // proficiency capture) — memoizing the snapshot for the scope's lifetime turns those into one HGETALL.
        // Save keeps this warm by merging in the rows it just changed rather than invalidating it, since the
        // prefetch case reads again *after* a Save — an invalidate-only memo would still pay a second read there.
        private (int PlayerId, CachedPlayerProgress Progress)? _memo;

        // Redis can't represent a hash key with zero fields, so a brand-new player's empty DB reload writes
        // this instead — a field name sharing no prefix with the row kinds below (S_/C_/P_), so
        // FromHashFields silently ignores it on every read. See GetCachedProgress for why the key must exist.
        private static readonly Dictionary<string, string> PresenceMarkerFields = new() { ["_"] = "1" };

        public async Task<PlayerProgress> Load(Player player, CancellationToken cancellationToken = default)
        {
            var cached = await GetCachedProgress(player.Id, cancellationToken);
            return new PlayerProgress(
                player,
                cached.Statistics.Select(ToCoreStatistic),
                cached.Challenges.Select(c => ToCoreChallenge(c, player.Id)),
                cached.Proficiencies.Select(ToCoreProficiency))
            {
                WriteSequence = cached.WriteSequence,
            };
        }

        public async Task<List<CoreStat>> GetStatistics(int playerId, CancellationToken cancellationToken = default)
        {
            var cached = await GetCachedProgress(playerId, cancellationToken);
            return cached.Statistics.Select(ToCoreStatistic).ToList();
        }

        public async Task<List<CoreChallenge>> GetChallenges(int playerId, CancellationToken cancellationToken = default)
        {
            var cached = await GetCachedProgress(playerId, cancellationToken);
            return cached.Challenges.Select(c => ToCoreChallenge(c, playerId)).ToList();
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

            // One sequence per enqueuing save, drawn from this aggregate's own counter rather than the player's
            // — the two are separate enqueues writing disjoint tables, so they are ordered independently (#2473).
            // Advanced only past the no-dirty-rows early return above, so a save that enqueues nothing consumes
            // no sequence. The value is stamped on the envelope and carried into the cache advance below, so a
            // reconnect reseeds the counter from it rather than restarting and re-stamping used values.
            changed.WriteSequence = progress.AdvanceWriteSequence();

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
                Sequence = changed.WriteSequence,
            };
            _updateBatch.Add(envelope);

            // The envelope above already captured this save's changed rows, so clearing the dirty tracking
            // now is safe: a second save of this same aggregate (without further mutation) enqueues nothing.
            progress.AcceptChanges();

            // The cache is the source of truth, but it is stored as a Redis hash keyed by row identity, so
            // advancing it only needs to HSET the rows this save actually touched (changed, captured now off
            // the live aggregate so a deferred advance still reflects this save's state) rather than
            // re-serializing every row the player has ever earned (#1635). The advance only ever holds this
            // save's dirty rows — a partial view — so it must not resurrect the key if it vanished between the
            // load and now (Redis eviction under memory pressure, or an operator delete): recreating the hash
            // from a partial view would silently shadow every other row the player holds behind a present key
            // that never falls through to the DB (#1761). GetCachedProgress's miss-reload path is the one
            // place that legitimately creates the key, since it rebuilds the whole set from the DB first.
            var fields = ToHashFields(changed);
            void AdvanceCache()
            {
                _cache.HashSetIfExistsAndForget(ProgressKey(playerId), fields, ProgressCacheTtl);

                // Keep this scope's memo (if one is warm) in sync with what was just persisted, so a later
                // read in the same command sees this save's rows immediately rather than racing the cache
                // advance's fire-and-forget HSET above. Bundled with the advance (rather than run right after
                // AcceptChanges) so a failed/deferred flush can never leak an uncommitted row into the memo —
                // this only runs once the flush this save's event rode has actually succeeded.
                if (_memo is { } memo && memo.PlayerId == playerId)
                {
                    MergeInto(memo.Progress, changed);
                }
            }

            if (_updateBatch.PlayerSaveInProgress)
            {
                // Riding the in-flight player save's single flush (the live battle-completion hot path): the
                // event is already buffered above; defer the cache advance so SavePlayer runs it only after
                // that flush enqueues the event, collapsing both writes onto one queue round-trip (#1237).
                _updateBatch.OnFlushed(AdvanceCache);
            }
            else
            {
                // Standalone progress save (no player-save batch scope is open): flush our own event, then
                // advance the cache — preserving publish-before-cache so the write is never stranded. FlushAsync
                // leaves the event buffered for the next flush attempt if the publish itself fails, rather than
                // losing it (#1494). A genuine (non-cancellation) flush failure is wrapped the same way
                // SavePlayer's is (PlayerPersistenceFlushFailedException), so the socket layer forces the
                // connection's in-memory Player to reload afterward — otherwise the caller's already-applied
                // in-memory mutations would silently ride along un-persisted and could be re-credited by a
                // same-connection retry (#1819).
                try
                {
                    await _updateBatch.FlushAsync(_pubsub, cancellationToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    throw new PlayerPersistenceFlushFailedException(ex);
                }

                AdvanceCache();
            }
        }

        private async Task<CachedPlayerProgress> GetCachedProgress(int playerId, CancellationToken cancellationToken)
        {
            if (_memo is { } memo && memo.PlayerId == playerId)
            {
                return memo.Progress;
            }

            var key = ProgressKey(playerId);
            // Sliding expiration: a hit refreshes the idle TTL so an active player never ages out, folded into
            // the same round trip as the read (#2019) rather than a separate fire-and-forget expire.
            var raw = await _cache.HashGetAllAndRefreshExpiry(key, ProgressCacheTtl, cancellationToken);
            CachedPlayerProgress? progress = null;
            if (raw is not null)
            {
                try
                {
                    progress = FromHashFields(raw, playerId);
                }
                catch (JsonException ex)
                {
                    // A row that no longer deserializes (e.g. a shape change to one of the cached row models'
                    // required members) is corruption, not data — the row models are all-required specifically so
                    // this throws instead of silently defaulting a shrunken/zeroed row that Save would then upsert
                    // to Postgres as an absolute value. Postgres remains the durable copy, so this self-heals the
                    // same way PlayerRepository.GetPlayer treats a corrupt player blob: delete the key and fall
                    // through to the DB reload below rather than locking progress reads for the rest of the TTL (#2000).
                    _logger.LogError(ex, "Cached progress for player {PlayerId} at key '{Key}' failed to deserialize; deleting the key and reloading from the database.", playerId, key);
                    await _cache.Delete(key, cancellationToken);
                }
            }

            if (progress is null)
            {
                // This reload just read the authoritative full state from the DB, so it's the one place that
                // may safely *create* the hash — Save's advance (above) only ever holds this save's dirty
                // rows and must not (#1761). A brand-new player with literally no rows yet leaves nothing to
                // HSET, but the key still needs to exist afterward so that player's first-ever Save (whose
                // dirty rows equal the player's entire state at that point) is able to create it rather than
                // silently no-op forever — so an empty reload writes the presence marker field instead.
                progress = await LoadFromDb(playerId, cancellationToken);
                var fields = ToHashFields(progress);
                _cache.HashSetAndForget(key, fields.Count > 0 ? fields : PresenceMarkerFields, ProgressCacheTtl);
            }

            _memo = (playerId, progress);
            return progress;
        }

        private const int StatisticRowKind = 0;
        private const int ChallengeRowKind = 1;
        private const int ProficiencyRowKind = 2;

        /// <summary>
        /// Cold path only (first read after a TTL lapse/eviction, or a brand-new player) — reads the three
        /// progress tables as one <c>UNION ALL</c> round trip instead of three sequential ones. EF Core cannot
        /// run independent queries concurrently on one <see cref="GameContext"/>, so batching them onto one
        /// round trip means projecting all three onto one shared anonymous shape and
        /// <see cref="Queryable.Concat{T}"/>-ing them (the shape a translatable set operation requires) rather
        /// than awaiting each in turn. Every column is populated for every row kind — defaulted rather than left
        /// null on the kinds that don't use it — discriminated by <c>Kind</c> on the way back out, so the
        /// reconstruction below never needs the null-forgiving operator.
        /// </summary>
        private async Task<CachedPlayerProgress> LoadFromDb(int playerId, CancellationToken cancellationToken)
        {
            var statistics = _context.PlayerStatistics
                .AsNoTracking()
                .Where(ps => ps.PlayerId == playerId)
                .Select(ps => new { Kind = StatisticRowKind, Id = ps.StatisticTypeId, EntityId = ps.EntityId, Level = 0, Amount = ps.Value, Completed = false, CompletedAt = (DateTime?)null });

            var challenges = _context.PlayerChallenges
                .AsNoTracking()
                .Where(pc => pc.PlayerId == playerId)
                .Select(pc => new { Kind = ChallengeRowKind, Id = pc.ChallengeId, EntityId = (int?)null, Level = 0, Amount = pc.Progress, Completed = pc.Completed, CompletedAt = pc.CompletedAt });

            var proficiencies = _context.PlayerProficiencies
                .AsNoTracking()
                .Where(pp => pp.PlayerId == playerId)
                .Select(pp => new { Kind = ProficiencyRowKind, Id = pp.ProficiencyId, EntityId = (int?)null, Level = pp.Level, Amount = pp.Xp, Completed = false, CompletedAt = (DateTime?)null });

            var rows = await statistics.Concat(challenges).Concat(proficiencies).ToListAsync(cancellationToken);

            // The write sequence is left at its Unsequenced default: nothing persists it yet, so a cold load
            // reseeds the counter from 0 and the next save stamps 1. Seeding it from the player's highest
            // persisted watermark arrives with the table that holds them (#2474) — safe to defer, because
            // nothing consumes the stamp until that same issue lands.
            var progress = new CachedPlayerProgress();
            foreach (var row in rows)
            {
                switch (row.Kind)
                {
                    case StatisticRowKind:
                        progress.Statistics.Add(new CachedPlayerStatistic { StatisticTypeId = row.Id, EntityId = row.EntityId, Value = row.Amount });
                        break;
                    case ChallengeRowKind:
                        progress.Challenges.Add(new CachedPlayerChallenge { ChallengeId = row.Id, Progress = row.Amount, Completed = row.Completed, CompletedAt = row.CompletedAt });
                        break;
                    case ProficiencyRowKind:
                        progress.Proficiencies.Add(new CachedPlayerProficiency { ProficiencyId = row.Id, Level = row.Level, Xp = row.Amount });
                        break;
                }
            }

            return progress;
        }

        private static CoreStat ToCoreStatistic(CachedPlayerStatistic cached) => new()
        {
            Type = (EStatisticType)cached.StatisticTypeId,
            EntityId = cached.EntityId,
            Value = cached.Value,
        };

        private CoreChallenge ToCoreChallenge(CachedPlayerChallenge cached, int playerId) =>
            new(
                OrphanedReferenceException.ResolveOrThrow(_challenges.GetChallenge, cached.ChallengeId, playerId, "challenge"),
                cached.Progress,
                cached.Completed,
                cached.CompletedAt);

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

        // Upserts a save's changed rows into the scope's memoized snapshot by natural key, mirroring the hash
        // fields' own identity (StatField/ChallengeField/ProficiencyField) so a row already in the memo is
        // overwritten in place rather than duplicated.
        private static void MergeInto(CachedPlayerProgress target, CachedPlayerProgress changed)
        {
            // A later Load in the same scope reseeds a fresh aggregate off this memo, so the counter has to
            // move with the rows — otherwise that aggregate would restart from the pre-save value and re-stamp
            // sequences this save already used.
            target.WriteSequence = Math.Max(target.WriteSequence, changed.WriteSequence);

            foreach (var stat in changed.Statistics)
            {
                var index = target.Statistics.FindIndex(s => s.StatisticTypeId == stat.StatisticTypeId && s.EntityId == stat.EntityId);
                if (index >= 0)
                {
                    target.Statistics[index] = stat;
                }
                else
                {
                    target.Statistics.Add(stat);
                }
            }

            foreach (var challenge in changed.Challenges)
            {
                var index = target.Challenges.FindIndex(c => c.ChallengeId == challenge.ChallengeId);
                if (index >= 0)
                {
                    target.Challenges[index] = challenge;
                }
                else
                {
                    target.Challenges.Add(challenge);
                }
            }

            foreach (var proficiency in changed.Proficiencies)
            {
                var index = target.Proficiencies.FindIndex(p => p.ProficiencyId == proficiency.ProficiencyId);
                if (index >= 0)
                {
                    target.Proficiencies[index] = proficiency;
                }
                else
                {
                    target.Proficiencies.Add(proficiency);
                }
            }
        }

        // Field-key prefixes double as the discriminator on read: each row's own kind is self-describing, so
        // FromHashFields never needs the caller to track kind alongside field name. The row's natural key
        // (type+entity / challenge id / proficiency id) makes each field name stable and collision-free across
        // saves, so a later HSET on the same row always overwrites the same field rather than appending a
        // duplicate. The ids are formatted invariantly because the field name is a persisted key every
        // instance in the fleet has to reproduce byte-for-byte, so it must not depend on the host locale.
        private static string StatField(int statisticTypeId, int? entityId) =>
            string.Create(CultureInfo.InvariantCulture, $"S_{statisticTypeId}_{entityId?.ToString(CultureInfo.InvariantCulture) ?? "n"}");
        private static string ChallengeField(int challengeId) => string.Create(CultureInfo.InvariantCulture, $"C_{challengeId}");
        private static string ProficiencyField(int proficiencyId) => string.Create(CultureInfo.InvariantCulture, $"P_{proficiencyId}");

        // Aggregate-level state rather than a row, so it gets a reserved field name outside the row-kind
        // prefixes (S_/C_/P_) — grouping with the "_" presence marker, and ignored by FromHashFields' row
        // parsing exactly as an unrecognised field always was, so a pre-upgrade instance reading a hash that
        // carries it is unaffected.
        private const string WriteSequenceField = "_seq";

        // Serializes each row as its own hash field, keyed by its natural identity, so Save can HSET only the
        // rows a save actually touched instead of rewriting the whole cached snapshot (#1635).
        private static Dictionary<string, string> ToHashFields(CachedPlayerProgress progress)
        {
            var fields = new Dictionary<string, string>();
            foreach (var stat in progress.Statistics)
            {
                fields[StatField(stat.StatisticTypeId, stat.EntityId)] = stat.Serialize();
            }

            foreach (var challenge in progress.Challenges)
            {
                fields[ChallengeField(challenge.ChallengeId)] = challenge.Serialize();
            }

            foreach (var proficiency in progress.Proficiencies)
            {
                fields[ProficiencyField(proficiency.ProficiencyId)] = proficiency.Serialize();
            }

            // Only written once the counter has actually advanced: a cold reload has nothing to seed it from
            // yet (#2474 adds the persisted watermarks it will read), and writing a 0 would waste a field
            // asserting the very default a missing field already means.
            if (progress.WriteSequence != DomainEventEnvelope.Unsequenced)
            {
                fields[WriteSequenceField] = progress.WriteSequence.ToString(CultureInfo.InvariantCulture);
            }

            return fields;
        }

        private CachedPlayerProgress FromHashFields(Dictionary<string, string> fields, int playerId)
        {
            var progress = new CachedPlayerProgress();
            foreach (var (field, value) in fields)
            {
                if (field.StartsWith("S_", StringComparison.Ordinal))
                {
                    // A row that deserializes to null (e.g. a literal "null" blob) is corruption exactly like a
                    // required-member mismatch — throwing here (rather than silently skipping) routes it through
                    // the same delete-and-reload self-heal instead of shadowing the row's DB copy behind a
                    // present-but-incomplete key (#2000).
                    var stat = value.Deserialize<CachedPlayerStatistic>() ?? throw new JsonException($"Progress field '{field}' deserialized to null.");
                    progress.Statistics.Add(stat);
                }
                else if (field.StartsWith("C_", StringComparison.Ordinal))
                {
                    var challenge = value.Deserialize<CachedPlayerChallenge>() ?? throw new JsonException($"Progress field '{field}' deserialized to null.");
                    progress.Challenges.Add(challenge);
                }
                else if (field.StartsWith("P_", StringComparison.Ordinal))
                {
                    var proficiency = value.Deserialize<CachedPlayerProficiency>() ?? throw new JsonException($"Progress field '{field}' deserialized to null.");
                    progress.Proficiencies.Add(proficiency);
                }
                else if (field == WriteSequenceField)
                {
                    // An unparseable counter is treated as absent rather than as corruption: unlike a row it
                    // carries no player data, so discarding the whole key over it would throw away real progress
                    // rows to salvage an ordering hint. But it is logged rather than swallowed — the reseed
                    // restarts the session at 1 against a watermark that may already be far higher, which under
                    // the #2474 guard rejects every write that session makes until the counter climbs back past
                    // it. Silent is the one thing that failure must not be.
                    if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var sequence))
                    {
                        progress.WriteSequence = sequence;
                    }
                    else
                    {
                        _logger.LogWarning(
                            "Cached progress for player {PlayerId} carried an unparseable write sequence '{RawValue}'; reseeding the counter from 0. Writes from this session may be rejected as stale until it catches up.",
                            playerId,
                            value);
                    }
                }
            }

            return progress;
        }
    }
}
