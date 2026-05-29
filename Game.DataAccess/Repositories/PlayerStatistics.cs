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
    }
}
