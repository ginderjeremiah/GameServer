using GameCore.DataAccess;
using GameCore.Entities;
using GameCore.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace DataAccess.Repositories
{
    internal class ItemMods : BaseRepository, IItemMods
    {
        private static List<ItemMod>? _allMods;
        private static readonly List<Dictionary<int, IEnumerable<ItemMod>>?> _itemModsBySlot = new();
        private static readonly object _lockForItem = new();

        public ItemMods(IDatabaseService database) : base(database) { }

        public async Task<IEnumerable<ItemMod>> AllItemModsAsync(bool refreshCache = false)
        {
            if (_allMods is null || refreshCache)
            {
                _allMods = await Database.ItemMods.Include(im => im.ItemModAttributes).ToListAsync();
            }
            return _allMods;
        }

        public Dictionary<int, IEnumerable<ItemMod>> GetModsForItemBySlot(int itemId)
        {
            if (itemId >= _itemModsBySlot.Count || _itemModsBySlot[itemId] is null)
            {
                lock (_lockForItem)
                {
                    for (int i = _itemModsBySlot.Count; i <= itemId + 1; i++)
                    {
                        _itemModsBySlot.Add(null);
                    }
                    _itemModsBySlot[itemId] ??= ModsForItemBySlot(itemId);
                }
            }
            return _itemModsBySlot[itemId];
        }

        public async Task<ItemMod?> GetItemModAsync(int itemModId)
        {
            var itemMods = (await AllItemModsAsync()).ToList();
            return itemMods.Count > itemModId ? null : itemMods[itemModId];
        }

        private Dictionary<int, IEnumerable<ItemMod>> ModsForItemBySlot(int itemId)
        {
            return Database.Items
                .Where(i => i.Id == itemId)
                .Include(i => i.Tags)
                    .ThenInclude(t => t.ItemMods)
                .SelectMany(i => i.Tags.SelectMany(t => t.ItemMods))
                .Distinct()
                .GroupBy(im => im.SlotTypeId)
                .ToDictionary(grp => grp.Key, grp => grp.AsEnumerable());
        }
    }
}
