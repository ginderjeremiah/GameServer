using Game.Abstractions.DataAccess;
using Game.DataAccess.Mapping;
using Game.DataAccess.Repositories.Caching;
using Game.Infrastructure.Entities;
using Contracts = Game.Abstractions.Contracts;
using CoreItemMod = Game.Core.Items.ItemMod;

namespace Game.DataAccess.Repositories
{
    internal class ItemMods(ItemModsCacheHolder holder) : IItemMods, IItemModEntityCache
    {
        private IReadOnlyList<ItemMod> Entities => holder.Current;

        public List<Contracts.ItemMod> All()
        {
            return [.. Entities.Select(ItemMapper.ModToContract)];
        }

        public bool ValidateItemModId(int itemModId)
        {
            return itemModId >= 0 && itemModId < Entities.Count;
        }

        public ItemMod? LookupItemMod(int itemModId)
        {
            var itemMods = Entities;
            return itemMods.Count <= itemModId || itemModId < 0 ? null : itemMods[itemModId];
        }

        public CoreItemMod GetItemMod(int itemModId)
        {
            return ItemMapper.ModToCore(Entities[itemModId]);
        }
    }
}
