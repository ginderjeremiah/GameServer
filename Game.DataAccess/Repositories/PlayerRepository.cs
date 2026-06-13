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
        /// out of Redis instead of occupying memory forever. The 48h budget mirrors the refresh-token
        /// lifetime: a player idle long enough for the key to lapse can no longer hold a live session, so
        /// the only cost of expiry is one transparent DB reload on their next access (the cache-miss path
        /// in <see cref="GetPlayer"/>). It also dwarfs the sub-second write-behind queue-drain window, so a
        /// refreshed key never expires mid-drain (see docs/backend.md → Caching and Pub/Sub).
        /// </summary>
        private static readonly TimeSpan PlayerCacheTtl = TimeSpan.FromHours(48);

        private readonly GameContext _context;
        private readonly ICacheService _cache;
        private readonly IDomainEventDispatcher _dispatcher;
        private readonly IItems _items;
        private readonly IItemMods _itemMods;
        private readonly ISkills _skills;

        public PlayerRepository(
            GameContext context,
            ICacheService cache,
            IDomainEventDispatcher dispatcher,
            IItems items,
            IItemMods itemMods,
            ISkills skills)
        {
            _context = context;
            _cache = cache;
            _dispatcher = dispatcher;
            _items = items;
            _itemMods = itemMods;
            _skills = skills;
        }

        public async Task<Player?> GetPlayer(int playerId)
        {
            var playerKey = $"{PlayerPrefix}_{playerId}";
            var player = await _cache.Get<Player>(playerKey);
            if (player is null)
            {
                player = await GetPlayerFromDb(playerId);
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

        public async Task SavePlayer(Player player)
        {
            await _dispatcher.DispatchAsync(player);

            var playerKey = $"{PlayerPrefix}_{player.Id}";

            _cache.SetAndForget(playerKey, player, PlayerCacheTtl);
        }

        private async Task<Player?> GetPlayerFromDb(int playerId)
        {
            // Only the player-specific relational data is fetched here; the reference-data portion
            // (item/skill/mod definitions and their attributes) is resolved from the in-memory cached
            // catalogs in PlayerMapper.ToCore, avoiding redundant deep joins on every player load.
            var entity = await _context.Players
                .AsNoTracking()
                .Include(p => p.PlayerAttributes)
                .Include(p => p.PlayerSkills)
                .Include(p => p.UnlockedItems)
                .Include(p => p.UnlockedMods)
                .Include(p => p.AppliedMods)
                .Include(p => p.LogPreferences)
                .AsSplitQuery()
                .FirstOrDefaultAsync(p => p.Id == playerId);

            return entity is null ? null : PlayerMapper.ToCore(entity, _items, _itemMods, _skills);
        }
    }

}
