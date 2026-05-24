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

        public async Task UpdateProgress(int playerId, int challengeId, int progress)
        {
            var entity = await _context.PlayerChallenges
                .FirstOrDefaultAsync(pc => pc.PlayerId == playerId && pc.ChallengeId == challengeId);

            if (entity is null)
            {
                entity = new PlayerChallenge
                {
                    PlayerId = playerId,
                    ChallengeId = challengeId,
                    Progress = progress,
                    Completed = false,
                };
                _context.PlayerChallenges.Add(entity);
            }
            else
            {
                entity.Progress = progress;
            }

            await _context.SaveChangesAsync();
        }

        public async Task CompleteChallenge(int playerId, int challengeId)
        {
            var entity = await _context.PlayerChallenges
                .FirstOrDefaultAsync(pc => pc.PlayerId == playerId && pc.ChallengeId == challengeId);

            if (entity is null)
            {
                entity = new PlayerChallenge
                {
                    PlayerId = playerId,
                    ChallengeId = challengeId,
                    Progress = 0,
                    Completed = true,
                    CompletedAt = DateTime.UtcNow,
                };
                _context.PlayerChallenges.Add(entity);
            }
            else
            {
                entity.Completed = true;
                entity.CompletedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();
        }
    }
}
