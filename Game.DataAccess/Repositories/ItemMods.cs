using Game.Abstractions.DataAccess;
using Game.Abstractions.Entities;
using Game.Infrastructure.Database;

using Microsoft.EntityFrameworkCore;

namespace Game.DataAccess.Repositories
{
    internal class ItemMods : IItemMods
    {
        private static List<ItemMod>? _allMods;
        private static readonly List<Dictionary<int, IEnumerable<ItemMod>>?> _itemModsByType = [];
        private static readonly object _lockForItem = new();

        private readonly GameContext _context;

        public ItemMods(GameContext context)
        {
            _context = context;
        }

        public List<ItemMod> All(bool refreshCache = false)
        {
            if (_allMods is null || refreshCache)
            {
                _allMods = _context.ItemMods
                    .Include(im => im.ItemModAttributes)
                    .AsNoTracking()
                    .ToList();
            }
            return _allMods;
        }

        public Dictionary<int, IEnumerable<ItemMod>> GetModsForItemByType(int itemId)
        {
            var mods = itemId >= _itemModsByType.Count ? null : _itemModsByType[itemId];
            if (mods is null)
            {
                lock (_lockForItem)
                {
                    for (int i = _itemModsByType.Count; i <= itemId + 1; i++)
                    {
                        _itemModsByType.Add(null);
                    }

                    mods = _itemModsByType[itemId];
                    if (mods is null)
                    {
                        mods = ModsForItemByType(itemId);
                        _itemModsByType[itemId] = mods;
                        return mods;
                    }
                }
            }

            return mods;
        }

        public ItemMod? GetItemMod(int itemModId)
        {
            var itemMods = All();
            return itemMods.Count <= itemModId ? null : itemMods[itemModId];
        }

        private Dictionary<int, IEnumerable<ItemMod>> ModsForItemByType(int itemId)
        {
            return _context.Items
                .Where(i => i.Id == itemId)
                .Include(i => i.Tags)
                    .ThenInclude(t => t.ItemMods)
                .SelectMany(i => i.Tags.SelectMany(t => t.ItemMods))
                .Distinct()
                .GroupBy(im => im.ItemModTypeId)
                .AsNoTracking()
                .ToDictionary(grp => grp.Key, grp => grp.AsEnumerable());
        }
    }
}
