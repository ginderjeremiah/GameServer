using Game.Abstractions.DataAccess;
using Game.Abstractions.Entities;
using Game.DataAccess.Mapping;
using Game.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Contracts = Game.Abstractions.Contracts;
using CoreItemMod = Game.Core.Items.ItemMod;

namespace Game.DataAccess.Repositories
{
    internal class ItemMods : IItemMods
    {
        private static List<ItemMod>? _allMods;

        private readonly GameContext _context;

        public ItemMods(GameContext context)
        {
            _context = context;
        }

        public void InvalidateCache() => _allMods = null;

        private List<ItemMod> AllEntities(bool refreshCache = false)
        {
            if (_allMods is null || refreshCache)
            {
                _allMods = _context.ItemMods
                    .Include(im => im.ItemModAttributes)
                    .Include(im => im.Tags)
                    .AsNoTracking()
                    .OrderBy(im => im.Id)
                    .ToList();
            }
            return _allMods;
        }

        public List<Contracts.ItemMod> All(bool refreshCache = false)
        {
            return [.. AllEntities(refreshCache).Select(ItemMapper.ModToContract)];
        }

        public ItemMod? LookupItemMod(int itemModId)
        {
            var itemMods = AllEntities();
            return itemMods.Count <= itemModId || itemModId < 0 ? null : itemMods[itemModId];
        }

        public CoreItemMod GetItemMod(int itemModId)
        {
            return ItemMapper.ModToCore(AllEntities()[itemModId]);
        }
    }
}
