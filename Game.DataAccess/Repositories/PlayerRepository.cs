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
            var player = await _cache.Get<Player>(playerKey, cancellationToken);
            if (player is null)
            {
                player = await GetPlayerFromDb(playerId, cancellationToken);
                if (player is not null)
                {
                    _cache.SetAndForget(playerKey, player, PlayerCacheTtl);
                }
            }
            else
            {
                // Sliding expiration: a cache hit refreshes the idle TTL so an active player never ages out.
                _cache.ExpireAndForget(playerKey, PlayerCacheTtl);
            }

            return player;
        }

        public async Task SavePlayer(Player player, CancellationToken cancellationToken = default)
        {
            // Dispatching the player's events buffers each one into the scoped PlayerUpdateBatch (via
            // PlayerPersistencePublisher) rather than publishing it individually; flushing the whole batch
            // here as a single multi-value LPUSH collapses a multi-event save into one queue round-trip (#559).
            await _dispatcher.DispatchAsync(player, cancellationToken);
            await _pubsub.PublishBatch(Constants.PUBSUB_PLAYER_CHANNEL, Constants.PUBSUB_PLAYER_QUEUE, _updateBatch.Drain(), cancellationToken);

            var playerKey = $"{PlayerPrefix}_{player.Id}";

            _cache.SetAndForget(playerKey, player, PlayerCacheTtl);
        }

        private async Task<Player?> GetPlayerFromDb(int playerId, CancellationToken cancellationToken)
        {
            // IncludePlayerGraph applies the full navigation graph PlayerMapper.ToCore reads, so the contract
            // is enforced structurally rather than per-query. The reference-data portion (item/skill/mod
            // definitions) is resolved from the in-memory cached catalogs in the mapper, not joined here.
            var entity = await _context.Players
                .AsNoTracking()
                .IncludePlayerGraph()
                .FirstOrDefaultAsync(p => p.Id == playerId, cancellationToken);

            return entity is null ? null : PlayerMapper.ToCore(entity, _items, _itemMods, _skills);
        }
    }

}
