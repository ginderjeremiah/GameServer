using Game.Abstractions.DataAccess;
using Game.Core.Items;
using Game.Infrastructure.Database;

namespace Game.DataAccess.Repositories
{
    internal class Inventories : IInventories
    {
        private readonly GameContext _context;
        private readonly ArrayDataCache<Item> _itemCache;
        private readonly ArrayDataCache<ItemMod> _itemModCache;

        public Inventories(GameContext context, ArrayDataCache<Item> itemCache, ArrayDataCache<ItemMod> itemModCache)
        {
            _context = context;
            _itemCache = itemCache;
            _itemModCache = itemModCache;
        }

        //public async Task<Inventory> GetPlayerInventory(int playerId)
        //{
        //    var inventory = new Inventory();
        //    var player = await _context.Players
        //        .Include(p => p.InventoryItems)
        //            .ThenInclude(ii => ii.InventoryItemMods)
        //                .ThenInclude(iim => iim.ItemModSlot)
        //        .FirstOrDefaultAsync(p => p.Id == playerId);

        //    if (player is not null)
        //    {
        //        foreach (var inventoryItem in player.InventoryItems)
        //        {
        //            var item = _itemCache.Data[inventoryItem.ItemId];
        //            foreach (var inventoryItemMod in inventoryItem.InventoryItemMods)
        //            {
        //                var itemMod = _itemModCache.Data[inventoryItemMod.ItemModId];
        //                var slot = item.ModSlots.FirstOrDefault(slot => slot.Index == inventoryItemMod.ItemModSlot.Index);
        //                if (slot != null)
        //                {
        //                    slot.ItemMod = itemMod;
        //                }
        //            }
        //        }
        //    }

        //    return inventory;
        //}

        //public async Task<int> AddInventoryItem(Item inventoryItem)
        //{
        //    _context.Add(inventoryItem);
        //    await _context.SaveChangesAsync();
        //    return inventoryItem.Id;
        //}

        //public async Task UpdateInventoryItemSlots(int playerId, IEnumerable<Item> inventoryItems)
        //{
        //    await _context.Players.Where(p => p.Id == playerId).SelectMany(p => p.InventoryItems).ForEachAsync(item =>
        //    {
        //        var match = inventoryItems.FirstOrDefault(i => i.Id == item.Id);
        //        if (match is null)
        //        {
        //            _context.Remove(item);
        //        }
        //        else
        //        {
        //            item.InventorySlotNumber = match.InventorySlotNumber;
        //            item.Equipped = match.Equipped;
        //            _context.Update(item);
        //        }
        //    });

        //    await _context.SaveChangesAsync();
        //}
    }
}
