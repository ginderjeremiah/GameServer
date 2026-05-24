using Game.Abstractions.DataAccess;
using Game.Abstractions.Entities;
using Game.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace Game.DataAccess.Repositories
{
    internal class UnlockedItems(GameContext context) : IUnlockedItems
    {
        private readonly GameContext _context = context;

        public async Task<List<UnlockedItem>> GetUnlockedItems(int playerId)
        {
            return await _context.UnlockedItems
                .Where(ui => ui.PlayerId == playerId)
                .Include(ui => ui.Item)
                .ToListAsync();
        }

        public async Task UnlockItem(int playerId, int itemId)
        {
            var exists = await _context.UnlockedItems
                .AnyAsync(ui => ui.PlayerId == playerId && ui.ItemId == itemId);

            if (!exists)
            {
                _context.UnlockedItems.Add(new UnlockedItem
                {
                    PlayerId = playerId,
                    ItemId = itemId,
                });
                await _context.SaveChangesAsync();
            }
        }

        public async Task EquipItem(int playerId, int itemId, int equipmentSlotId)
        {
            // Unequip anything currently in the target slot
            var currentlyEquipped = await _context.UnlockedItems
                .FirstOrDefaultAsync(ui => ui.PlayerId == playerId && ui.EquipmentSlotId == equipmentSlotId);

            if (currentlyEquipped is not null)
            {
                currentlyEquipped.EquipmentSlotId = null;
            }

            var entity = await _context.UnlockedItems
                .FirstOrDefaultAsync(ui => ui.PlayerId == playerId && ui.ItemId == itemId);

            if (entity is not null)
            {
                entity.EquipmentSlotId = equipmentSlotId;
                await _context.SaveChangesAsync();
            }
        }

        public async Task UnequipItem(int playerId, int itemId)
        {
            var entity = await _context.UnlockedItems
                .FirstOrDefaultAsync(ui => ui.PlayerId == playerId && ui.ItemId == itemId);

            if (entity is not null)
            {
                entity.EquipmentSlotId = null;
                await _context.SaveChangesAsync();
            }
        }
    }
}
