using Game.Core.DataAccess;
using Game.Core.Entities;
using Game.Core.Infrastructure;
using Game.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace Game.DataAccess.Repositories
{
    internal class Players : IPlayers
    {
        private static string PlayerPrefix => Constants.CACHE_PLAYER_PREFIX;

        private readonly GameContext _context;
        private readonly ICacheService _cache;
        private readonly DataProviderSynchronizer _synchronizer;


        public Players(GameContext context, ICacheService cache, DataProviderSynchronizer synchronizer)
        {
            _context = context;
            _cache = cache;
            _synchronizer = synchronizer;
        }

        public async Task<Player?> GetPlayer(string userName)
        {
            var player = await PlayersWithRelatedData().FirstOrDefaultAsync(p => p.UserName == userName);
            if (player is not null)
            {
                _cache.SetAndForget($"{PlayerPrefix}_{player.Id}", player);
            }

            return player;
        }

        public async Task<Player?> GetPlayer(int playerId)
        {
            var playerKey = $"{PlayerPrefix}_{playerId}";
            var player = await _cache.GetAsync<Player>(playerKey);
            if (player is null)
            {
                player = await PlayersWithRelatedData().FirstOrDefaultAsync(p => p.Id == playerId);
                if (player is not null)
                {
                    _cache.SetAndForget(playerKey, player);
                }
            }

            return player;
        }

        public async Task SavePlayer(Player player, bool playerDirty, bool inventoryDirty, bool skillsDirty)
        {
            var playerKey = $"{PlayerPrefix}_{player.Id}";
            _cache.SetAndForget(playerKey, player);
            if (inventoryDirty)
            {
                await _synchronizer.SynchronizeInventory(playerKey);
            }
            if (playerDirty)
            {
                await _synchronizer.SynchronizePlayerData(playerKey);
            }
            if (skillsDirty)
            {
                await _synchronizer.SynchronizeSkills(playerKey);
            }
        }

        private IQueryable<Player> PlayersWithRelatedData()
        {
            return _context.Players
                .AsNoTracking()
                .Include(p => p.InventoryItems)
                    .ThenInclude(ii => ii.InventoryItemMods)
                .Include(p => p.PlayerAttributes)
                .Include(p => p.PlayerSkills)
                .Include(p => p.LogPreferences);
        }
    }
}
