using Game.Abstractions.DataAccess;
using Game.Abstractions.Infrastructure;
using Game.Core.Players;
using Game.Core.Players.Inventories;
using Game.DataAccess.Mapping;
using Game.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using EntityPlayerAttribute = Game.Abstractions.Entities.PlayerAttribute;

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

        public async Task<Player?> GetPlayer(int playerId)
        {
            var playerKey = $"{PlayerPrefix}_{playerId}";
            var player = await _cache.GetAsync<Player>(playerKey);
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
            var entity = await _context.Players
                .Include(p => p.PlayerAttributes)
                .FirstOrDefaultAsync(p => p.Id == player.Id);

            if (entity is null) return;

            // Sync scalar fields
            entity.Level = player.Level;
            entity.Exp = player.Exp;
            entity.StatPointsGained = player.StatPoints.StatPointsGained;
            entity.StatPointsUsed = player.StatPoints.StatPointsUsed;
            entity.CurrentZoneId = player.CurrentZoneId;

            // Sync stat allocations — remove old rows, add current
            foreach (var attr in entity.PlayerAttributes.ToList())
            {
                _context.Remove(attr);
            }
            foreach (var allocation in player.StatPoints.StatAllocations)
            {
                entity.PlayerAttributes.Add(new EntityPlayerAttribute
                {
                    PlayerId = player.Id,
                    AttributeId = (int)allocation.Attribute,
                    Amount = (decimal)allocation.Amount,
                });
            }

            // Keep Redis cache in sync
            _cache.SetAndForget($"{PlayerPrefix}_{player.Id}", player);
        }

        private async Task<Player?> GetPlayerFromDb(int playerId)
        {
            var entity = await _context.Players
                .AsNoTracking()
                .Include(p => p.PlayerAttributes)
                .Include(p => p.PlayerSkills)
                    .ThenInclude(ps => ps.Skill)
                        .ThenInclude(s => s.SkillDamageMultipliers)
                .Include(p => p.InventoryItems)
                    .ThenInclude(ii => ii.Item)
                        .ThenInclude(i => i.ItemAttributes)
                .Include(p => p.InventoryItems)
                    .ThenInclude(ii => ii.Item)
                        .ThenInclude(i => i.ItemModSlots)
                .Include(p => p.InventoryItems)
                    .ThenInclude(ii => ii.InventoryItemMods)
                        .ThenInclude(iim => iim.ItemMod)
                            .ThenInclude(im => im!.ItemModAttributes)
                .AsSplitQuery()
                .FirstOrDefaultAsync(p => p.Id == playerId);

            return entity is null ? null : PlayerMapper.ToCore(entity);
        }
    }
}
