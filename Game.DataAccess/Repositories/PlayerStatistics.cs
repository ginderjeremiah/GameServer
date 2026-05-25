using Game.Abstractions.DataAccess;
using Game.Abstractions.Entities;
using Game.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace Game.DataAccess.Repositories
{
    internal class PlayerStatistics(GameContext context) : IPlayerStatistics
    {
        private readonly GameContext _context = context;

        public async Task<List<PlayerStatistic>> GetPlayerStatistics(int playerId)
        {
            return await _context.PlayerStatistics
                .Where(ps => ps.PlayerId == playerId)
                .ToListAsync();
        }

        public async Task<long> IncrementStatistic(int playerId, int statisticTypeId, int entityId, long amount)
        {
            var entity = await _context.PlayerStatistics
                .FirstOrDefaultAsync(ps =>
                    ps.PlayerId == playerId &&
                    ps.StatisticTypeId == statisticTypeId &&
                    ps.EntityId == entityId);

            if (entity is null)
            {
                entity = new PlayerStatistic
                {
                    PlayerId = playerId,
                    StatisticTypeId = statisticTypeId,
                    EntityId = entityId,
                    Value = amount,
                };
                _context.PlayerStatistics.Add(entity);
            }
            else
            {
                entity.Value += amount;
            }

            return entity.Value;
        }
    }
}
