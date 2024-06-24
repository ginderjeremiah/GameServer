using GameCore.DataAccess;
using GameCore.Entities;
using GameCore.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace DataAccess.Repositories
{
    internal class InventoryItems : BaseRepository, IInventoryItems
    {
        public static readonly object _inventoryLock = new();
        public static bool _processingInventoryQueue = false;
        public static readonly object _equippedLock = new();
        public static bool _processingEquippedQueue = false;

        public InventoryItems(IDatabaseService database) : base(database) { }

        public async Task<IEnumerable<InventoryItem>> GetInventoryAsync(int playerId)
        {
            var player = await Database.Players
                .Include(p => p.InventoryItems.Select(i => i.Item.ItemAttributes))
                .Include(p => p.InventoryItems.Select(i => i.InventoryItemMods.Select(im => im.ItemMod.ItemModAttributes)))
                .FirstOrDefaultAsync(p => p.Id == playerId);
            return player?.InventoryItems ?? Enumerable.Empty<InventoryItem>();
        }

        public int AddInventoryItem(InventoryItem inventoryItem)
        {
            Database.Insert(inventoryItem);
            return inventoryItem.Id;
        }

        public async Task UpdateInventoryItemSlotsAsync(int playerId, IEnumerable<InventoryItem> inventoryItems)
        {
            await Database.Players.SelectMany(p => p.InventoryItems).ForEachAsync(item =>
            {
                var match = inventoryItems.FirstOrDefault(i => i.Id == item.Id);
                if (match is null)
                {
                    Database.Delete(item);
                }
                else
                {
                    item.InventorySlotNumber = match.InventorySlotNumber;
                    item.Equipped = match.Equipped;
                    Database.Update(item);
                }
            });

            await Database.SaveChangesAsync();
        }
    }
}
