using Game.Core.DataAccess;
using Game.Core.Entities;
using Game.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace Game.DataAccess.Repositories
{
    internal class Items : IItems
    {
        private static List<Item>? _allItems;

        private readonly GameContext _context;

        public Items(GameContext context)
        {
            _context = context;
        }

        public List<Item> All(bool refreshCache = false)
        {
            if (_allItems is null || refreshCache)
            {
                _allItems = [.. _context.Items
                    .AsNoTracking()
                    .Include(i => i.ItemModSlots)
                    .Include(i => i.ItemAttributes)
                    .Include(i => i.Tags)
                    .OrderBy(i => i.Id)];
            }
            return _allItems;
        }

        public Item? GetItem(int itemId)
        {
            var items = All();
            return items.Count <= itemId ? null : items[itemId];
        }
    }
}
