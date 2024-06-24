using GameCore.DataAccess;
using GameCore.Entities;
using GameCore.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace DataAccess.Repositories
{
    internal class Items : BaseRepository, IItems
    {

        private static List<Item>? _allItems;
        public Items(IDatabaseService database) : base(database) { }

        public async Task<IEnumerable<Item>> AllItemsAsync(bool refreshCache = false)
        {
            if (_allItems is null || refreshCache)
            {
                _allItems = await Database.Items
                    .AsNoTracking()
                    .Include(i => i.ItemSlots)
                    .Include(i => i.ItemAttributes)
                    .Include(i => i.ItemCategory)
                    .Include(i => i.Tags)
                    .ToListAsync();
            }
            return _allItems;
        }

        public async Task<Item?> GetItemAsync(int itemId)
        {
            var items = (await AllItemsAsync()).ToList();
            return items.Count > itemId ? null : items[itemId];
        }
    }
}
