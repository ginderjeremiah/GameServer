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

        public PlayerRepository(GameContext context, ICacheService cache, IDomainEventDispatcher dispatcher)
        {
            _context = context;
            _cache = cache;
            _dispatcher = dispatcher;
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
            //TODO: remove a lot of these sub-includes and utilize the in-memory cached reference data where it's actually needed.
            var entity = await _context.Players
                .AsNoTracking()
                .Include(p => p.PlayerAttributes)
                .Include(p => p.PlayerSkills)
                    .ThenInclude(ps => ps.Skill)
                        .ThenInclude(s => s.SkillDamageMultipliers)
                .Include(p => p.UnlockedItems)
                    .ThenInclude(ui => ui.Item)
                        .ThenInclude(i => i.ItemAttributes)
                .Include(p => p.UnlockedItems)
                    .ThenInclude(ui => ui.Item)
                        .ThenInclude(i => i.ItemModSlots)
                .Include(p => p.UnlockedMods)
                .Include(p => p.AppliedMods)
                    .ThenInclude(am => am.ItemMod)
                        .ThenInclude(im => im.ItemModAttributes)
                .Include(p => p.LogPreferences)
                .AsSplitQuery()
                .FirstOrDefaultAsync(p => p.Id == playerId);

            return entity is null ? null : PlayerMapper.ToCore(entity);
        }
    }

}
