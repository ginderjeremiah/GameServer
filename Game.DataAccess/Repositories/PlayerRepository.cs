using Game.Abstractions.Auth;
using Game.Abstractions.DataAccess;
using Game.Abstractions.Infrastructure;
using Game.Core.Events;
using Game.Core.Players;
using Game.DataAccess.Mapping;
using Game.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace Game.DataAccess.Repositories
{
    internal class PlayerRepository : IPlayerRepository
    {
        private static string PlayerPrefix => Constants.CACHE_PLAYER_PREFIX;

        /// <summary>
        /// Idle TTL for the cached player aggregate. It is written on every save and load-miss re-cache
        /// and refreshed on every cache hit (sliding expiration), so an actively-playing player — whose
        /// socket commands read/write the key continuously — never expires, while a dormant player ages
        /// out of Redis instead of occupying memory forever. The budget mirrors the refresh-token
        /// lifetime (the shared <see cref="AuthConstants.RefreshTokenLifetime"/> anchor, also used by the
        /// session key — #537): a player idle long enough for the key to lapse can no longer hold a live
        /// session, so the only cost of expiry is one transparent DB reload on their next access (the
        /// cache-miss path in <see cref="GetPlayer"/>). It also dwarfs the sub-second write-behind
        /// queue-drain window, so a refreshed key never expires mid-drain (see docs/backend-persistence.md → Caching
        /// and Pub/Sub).
        /// </summary>
        private static readonly TimeSpan PlayerCacheTtl = AuthConstants.RefreshTokenLifetime;

        private readonly GameContext _context;
        private readonly ICacheService _cache;
        private readonly IPubSubService _pubsub;
        private readonly IDomainEventDispatcher _dispatcher;
        private readonly PlayerUpdateBatch _updateBatch;
        private readonly IItems _items;
        private readonly IItemMods _itemMods;
        private readonly ISkills _skills;

        public PlayerRepository(
            GameContext context,
            ICacheService cache,
            IPubSubService pubsub,
            IDomainEventDispatcher dispatcher,
            PlayerUpdateBatch updateBatch,
            IItems items,
            IItemMods itemMods,
            ISkills skills)
        {
            _context = context;
            _cache = cache;
            _pubsub = pubsub;
            _dispatcher = dispatcher;
            _updateBatch = updateBatch;
            _items = items;
            _itemMods = itemMods;
            _skills = skills;
        }

        public async Task<Player?> GetPlayer(int playerId, CancellationToken cancellationToken = default)
        {
            var playerKey = $"{PlayerPrefix}_{playerId}";
            // The cache and the database both yield the lean PlayerCacheModel, so the reference graph is
            // re-resolved from the in-memory catalogs through one rehydration path regardless of the source —
            // a cached player can never serve stale reference data (#1155).
            var model = await _cache.Get<PlayerCacheModel>(playerKey, cancellationToken);
            if (model is null)
            {
                model = await GetPlayerModelFromDb(playerId, cancellationToken);
                if (model is null)
                {
                    return null;
                }

                _cache.SetAndForget(playerKey, model, PlayerCacheTtl);
            }
            else
            {
                // Sliding expiration: a cache hit refreshes the idle TTL so an active player never ages out.
                _cache.ExpireAndForget(playerKey, PlayerCacheTtl);
            }

            return PlayerCacheMapper.ToCore(model, _items, _itemMods, _skills);
        }

        public async Task SavePlayer(Player player, CancellationToken cancellationToken = default)
        {
            // Dispatching the player's events buffers each one into the scoped PlayerUpdateBatch (via
            // PlayerPersistencePublisher) rather than publishing it individually; flushing the whole batch
            // here as a single multi-value LPUSH collapses a multi-event save into one queue round-trip (#559).
            // The dispatch runs inside a player-save window so a progress save it triggers (the live
            // battle-completion path: BattleCompletedEvent -> BattleStatisticsEventHandler -> progress save)
            // joins this batch instead of issuing its own round-trip, collapsing the player and progress
            // writes onto this one flush (#1237).
            //
            // A handler fault (e.g. a transient Redis/DB blip inside BattleStatisticsEventHandler, or an
            // API-layer notifier) is caught rather than left to escape here unwrapped: escaping here would skip
            // the flush below entirely, discarding whatever other handlers already buffered into the batch
            // before/around the failure. Instead the fault is captured and the flush still runs, then the fault
            // is rethrown (wrapped, see below) once the batch's already-buffered envelopes are safely enqueued (#1819).
            Exception? dispatchFault = null;
            using (_updateBatch.BeginPlayerSave())
            {
                try
                {
                    await _dispatcher.DispatchAsync(player, cancellationToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    dispatchFault = ex;
                }
            }

            // FlushAsync only drains the batch and runs deferred progress cache-advances (see RunFlushedCallbacks)
            // once the publish has actually succeeded, so a failed flush (a transient Redis blip) leaves both
            // buffered for the next save's flush instead of silently losing them (#1494). But this command's
            // scope (and its PlayerUpdateBatch) ends here regardless, so a genuine flush failure is wrapped in
            // a distinct type: the socket layer forces the connection's in-memory Player to reload afterward,
            // rather than silently carrying this save's mutations forward with no queued event to match (#1632).
            try
            {
                await _updateBatch.FlushAsync(_pubsub, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // A cancellation is a cooperative unwind rather than a persistence fault, so it propagates
                // unwrapped instead of being wrapped in this type — pinned by
                // PlayerWriteBehindTests.SavePlayer_PublishFails_PreservesTheEventForTheNextFlush, which expects
                // OperationCanceledException specifically and would fail if this were wrapped instead. A
                // cancellation reaching here can still leave this save's mutations stranded with no queued
                // event to match, so SocketHandler forces the same in-memory reload for a command that settles
                // via cancellation as it does for this exception, just classified by the settled task's status
                // rather than by catching this type (#1849).
                throw new PlayerPersistenceFlushFailedException(ex);
            }

            if (dispatchFault is not null)
            {
                // The flush above succeeded (whatever it carried), but the dispatch itself still failed, so the
                // aggregate's in-memory state may not match what actually got persisted — force the same reload
                // a flush failure triggers, and skip the cache blob write below (#1819).
                throw new PlayerPersistenceFlushFailedException(dispatchFault);
            }

            var playerKey = $"{PlayerPrefix}_{player.Id}";

            // Serialize the lean model rather than the aggregate, so the cached blob never holds reference data.
            // Deliberately fire-and-forget: the in-memory aggregate (not this blob) is the read-modify-write base
            // during a session — loaded once at connect and re-saved per command without being re-read — so a dropped
            // write is invisible to the live session and self-heals on the next save, while the awaited queue write
            // above still carries the change to Postgres. Awaiting this would put a Redis round-trip on the per-battle
            // hot path for no durability gain. See docs/backend-persistence.md (write-behind player cache).
            _cache.SetAndForget(playerKey, PlayerCacheMapper.ToCacheModel(player), PlayerCacheTtl);
        }

        private async Task<PlayerCacheModel?> GetPlayerModelFromDb(int playerId, CancellationToken cancellationToken)
        {
            // The projection pulls only the player's own relational columns (reference data reduced to ids,
            // re-resolved from the cached catalogs in PlayerCacheMapper.ToCore), so no navigation graph is
            // loaded and no reference-data definitions are joined here.
            return await _context.Players
                .Where(p => p.Id == playerId)
                .SelectPlayerCacheModel()
                .FirstOrDefaultAsync(cancellationToken);
        }
    }

}
