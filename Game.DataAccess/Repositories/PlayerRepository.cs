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
                    _cache.SetAndForget(playerKey, player);
                }
            }

            return player;
        }

        public async Task SavePlayer(Player player)
        {
            await _dispatcher.DispatchAsync(player);

            var playerKey = $"{PlayerPrefix}_{player.Id}";

            _cache.SetAndForget(playerKey, player);
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
