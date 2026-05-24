using Game.Abstractions.DataAccess;
using Game.Abstractions.Entities;
using Game.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace Game.DataAccess.Repositories
{
    internal class AppliedMods(GameContext context) : IAppliedMods
    {
        private readonly GameContext _context = context;

        public async Task<List<AppliedMod>> GetAppliedMods(int playerId)
        {
            return await _context.AppliedMods
                .Where(am => am.PlayerId == playerId)
                .Include(am => am.ItemMod)
                    .ThenInclude(im => im.ItemModAttributes)
                .ToListAsync();
        }

        public async Task ApplyMod(int playerId, int itemId, int itemModSlotId, int itemModId)
        {
            // Remove any existing mod in the same slot
            var existing = await _context.AppliedMods
                .FirstOrDefaultAsync(am =>
                    am.PlayerId == playerId &&
                    am.ItemId == itemId &&
                    am.ItemModSlotId == itemModSlotId);

            if (existing is not null)
            {
                _context.AppliedMods.Remove(existing);
            }

            _context.AppliedMods.Add(new AppliedMod
            {
                PlayerId = playerId,
                ItemId = itemId,
                ItemModSlotId = itemModSlotId,
                ItemModId = itemModId,
            });

            await _context.SaveChangesAsync();
        }

        public async Task RemoveMod(int playerId, int itemId, int itemModSlotId)
        {
            var entity = await _context.AppliedMods
                .FirstOrDefaultAsync(am =>
                    am.PlayerId == playerId &&
                    am.ItemId == itemId &&
                    am.ItemModSlotId == itemModSlotId);

            if (entity is not null)
            {
                _context.AppliedMods.Remove(entity);
                await _context.SaveChangesAsync();
            }
        }
    }
}
