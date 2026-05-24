using Game.Abstractions.DataAccess;
using Game.Abstractions.Entities;
using Game.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace Game.DataAccess.Repositories
{
    internal class UnlockedMods(GameContext context) : IUnlockedMods
    {
        private readonly GameContext _context = context;

        public async Task<List<UnlockedMod>> GetUnlockedMods(int playerId)
        {
            return await _context.UnlockedMods
                .Where(um => um.PlayerId == playerId)
                .ToListAsync();
        }

        public async Task UnlockMod(int playerId, int itemModId)
        {
            var exists = await _context.UnlockedMods
                .AnyAsync(um => um.PlayerId == playerId && um.ItemModId == itemModId);

            if (!exists)
            {
                _context.UnlockedMods.Add(new UnlockedMod
                {
                    PlayerId = playerId,
                    ItemModId = itemModId,
                });
                await _context.SaveChangesAsync();
            }
        }
    }
}
