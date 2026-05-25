using Game.Abstractions.DataAccess;
using Game.Abstractions.Infrastructure;
using Game.Core.Events;
using Game.Core.Players;
using Game.DataAccess.Mapping;
using Game.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Game.DataAccess.Repositories
{
    internal class PlayerRepository(GameContext context, ICacheService cache, IPubSubService pubsub) : IPlayerRepository
    {
        private static string PlayerPrefix => Constants.CACHE_PLAYER_PREFIX;

        private readonly GameContext _context = context;
        private readonly ICacheService _cache = cache;
        private readonly IPubSubService _pubsub = pubsub;

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
            var playerKey = $"{PlayerPrefix}_{player.Id}";
            _cache.SetAndForget(playerKey, player);

            foreach (var domainEvent in player.DomainEvents)
            {
                await _pubsub.Publish(
                    Constants.PUBSUB_PLAYER_CHANNEL,
                    Constants.PUBSUB_PLAYER_QUEUE,
                    SerializeEvent(domainEvent));
            }
        }

        private async Task<Player?> GetPlayerFromDb(int playerId)
        {
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

        private static string SerializeEvent(IDomainEvent domainEvent)
        {
            var wrapper = new DomainEventEnvelope
            {
                Type = domainEvent.GetType().Name,
                Payload = JsonSerializer.Serialize(domainEvent, domainEvent.GetType()),
            };

            return JsonSerializer.Serialize(wrapper);
        }
    }

    internal class DomainEventEnvelope
    {
        public required string Type { get; set; }
        public required string Payload { get; set; }
    }
}
