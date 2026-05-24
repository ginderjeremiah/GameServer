using Game.Abstractions.DataAccess;
using Game.Abstractions.Entities;
using Game.Core.Players.Inventories;
using Game.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace Game.DataAccess.Repositories
{
    internal class Inventories(GameContext context) : IInventories
    {
        private readonly GameContext _context = context;

        public async Task<int> AddInventoryItem(int playerId, int itemId, int slotNumber, int rating = 1)
        {
            var entity = new InventoryItem
            {
                PlayerId = playerId,
                ItemId = itemId,
                InventorySlotNumber = slotNumber,
                Rating = rating,
                Equipped = false,
            };
            _context.InventoryItems.Add(entity);
            await _context.SaveChangesAsync();
            return entity.Id;
        }

        public async Task UpdateInventoryItemSlots(int playerId, IEnumerable<IInventoryUpdate> updates)
        {
            var existing = await _context.InventoryItems
                .Where(ii => ii.PlayerId == playerId)
                .ToListAsync();

            foreach (var item in existing)
            {
                var match = updates.FirstOrDefault(u => u.Id == item.Id);
                if (match is null)
                {
                    _context.InventoryItems.Remove(item);
                }
                else
                {
                    item.InventorySlotNumber = match.SlotNumber;
                    item.Equipped = match.Equipped;
                }
            }
            // No explicit SaveChangesAsync here — IUnitOfWork.CommitAsync() handles the flush
            // at the end of the request/command pipeline.
        }
    }
}
