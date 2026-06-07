using Game.Abstractions.DataAccess;
using Game.Abstractions.Entities;
using Game.DataAccess.Mapping;
using Game.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Contracts = Game.Abstractions.Contracts;
using CoreItem = Game.Core.Items.Item;

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

        public void InvalidateCache() => _allItems = null;

        private List<Item> AllEntities(bool refreshCache = false)
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

        public List<Contracts.Item> All(bool refreshCache = false)
        {
            return [.. AllEntities(refreshCache).Select(ItemMapper.ToContract)];
        }

        public Item? LookupItem(int itemId)
        {
            var items = AllEntities();
            return items.Count <= itemId || itemId < 0 ? null : items[itemId];
        }

        public CoreItem GetItem(int itemId)
        {
            return ItemMapper.ToCore(AllEntities()[itemId]);
        }
    }
}
