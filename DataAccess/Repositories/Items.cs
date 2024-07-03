using GameCore.DataAccess;
using GameCore.Entities;
using GameInfrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace DataAccess.Repositories
{
    internal class Items : BaseRepository, IItems
    {

        private static List<Item>? _allItems;
        public Items(GameContext database) : base(database) { }

        public List<Item> AllItems(bool refreshCache = false)
        {
            if (_allItems is null || refreshCache)
            {
                _allItems = [.. Database.Items
                    .AsNoTracking()
                    .Include(i => i.ItemSlots)
                    .Include(i => i.ItemAttributes)
                    .Include(i => i.ItemCategory)
                    .Include(i => i.Tags)];
            }
            return _allItems;
        }

        public Item? GetItem(int itemId)
        {
            var items = AllItems();
            return items.Count > itemId ? null : items[itemId];
        }
    }
}
