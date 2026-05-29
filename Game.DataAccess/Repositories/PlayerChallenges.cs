using Game.Abstractions.DataAccess;
using Game.Abstractions.Entities;
using Game.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace Game.DataAccess.Repositories
{
    internal class PlayerChallenges(GameContext context) : IPlayerChallenges
    {
        private readonly GameContext _context = context;

        public async Task<List<PlayerChallenge>> GetPlayerChallenges(int playerId)
        {
            return await _context.PlayerChallenges
                .Where(pc => pc.PlayerId == playerId)
                .ToListAsync();
        }
    }
}
