using Game.Core.DataAccess;
using Game.Core.Entities;
using Game.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace Game.DataAccess.Repositories
{
    internal class InventoryItems : IInventoryItems
    {
        private readonly GameContext _context;

        public InventoryItems(GameContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<InventoryItem>> GetPlayerInventory(int playerId)
        {
            var player = await _context.Players
                .Include(p => p.InventoryItems.Select(i => i.Item.ItemAttributes))
                .Include(p => p.InventoryItems.Select(i => i.InventoryItemMods.Select(im => im.ItemMod.ItemModAttributes)))
                .FirstOrDefaultAsync(p => p.Id == playerId);

            return player?.InventoryItems ?? Enumerable.Empty<InventoryItem>();
        }

        public async Task<int> AddInventoryItem(InventoryItem inventoryItem)
        {
            _context.Add(inventoryItem);
            await _context.SaveChangesAsync();
            return inventoryItem.Id;
        }

        public async Task UpdateInventoryItemSlots(int playerId, IEnumerable<InventoryItem> inventoryItems)
        {
            await _context.Players.Where(p => p.Id == playerId).SelectMany(p => p.InventoryItems).ForEachAsync(item =>
            {
                var match = inventoryItems.FirstOrDefault(i => i.Id == item.Id);
                if (match is null)
                {
                    _context.Remove(item);
                }
                else
                {
                    item.InventorySlotNumber = match.InventorySlotNumber;
                    item.Equipped = match.Equipped;
                    _context.Update(item);
                }
            });

            await _context.SaveChangesAsync();
        }
    }
}
