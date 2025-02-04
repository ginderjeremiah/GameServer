using Game.Abstractions.DataAccess;
using Game.Abstractions.Infrastructure;
using Game.Core.Players;
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

        public async Task<Player?> GetPlayer(int playerId)
        {
            var playerKey = $"{PlayerPrefix}_{playerId}";
            var player = await _cache.GetAsync<Player>(playerKey);
            if (player is null)
            {
                var playerEntity = await GetPlayerFromDb(playerId);
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

        private async Task<Player?> GetPlayerFromDb(int playerId)
        {
            var player = _context.Players
                .Include(p => p.PlayerAttributes)
                .Include(p => p.LogPreferences)
                .Include(p => p.PlayerSkills)
                .Include(p => p.InventoryItems)
                .AsSplitQuery()
                .FirstOrDefault(p => p.Id == playerId);

            return new Player
            {
                Id = player.Id,
                Name = player.Name,
                UserName = player.UserName,
                Level = player.Level,
                Exp = player.Exp,
                Salt = player.Salt,
                PassHash = player.PassHash,
                StatPointsGained = player.StatPointsGained,
                StatPointsUsed = player.StatPointsUsed,
                PlayerAttributes = player.PlayerAttributes.Select(pa => new Core.Players.PlayerAttribute
                {
                    Id = pa.Id,
                    PlayerId = pa.PlayerId,
                    AttributeId = pa.AttributeId,
                    Amount = pa.Amount
                }).ToList(),
                LogPreferences = player.LogPreferences.Select(lp => new Core.Players.LogPreference
                {
                    Id = lp.Id,
                    PlayerId = lp.PlayerId,
                    LogSettingId = lp.LogSettingId,
                    Enabled = lp.Enabled
                }).ToList(),
                PlayerSkills = player.PlayerSkills.Select(ps => new Core.Players.PlayerSkill
                {
                    Id = ps.Id,
                    PlayerId = ps.PlayerId,
                    SkillId = ps.SkillId,
                    Selected = ps.Selected
                }).ToList(),
                InventoryItems = player.InventoryItems.Select(i => new Core.Items.InventoryItem
                {
                    Id = i.Id,
                    PlayerId = i.PlayerId,
                    ItemId = i.ItemId,
                    Amount = i.Amount
                }).ToList()
            }
    }
    }
