using Game.Abstractions.DataAccess;
using Game.DataAccess.Mapping;
using Game.DataAccess.Repositories.Caching;
using Game.Infrastructure.Entities;
using Contracts = Game.Abstractions.Contracts;
using CoreItem = Game.Core.Items.Item;

namespace Game.DataAccess.Repositories
{
    internal class Items(ItemsCacheHolder holder) : IItems, IItemEntityCache
    {
        private IReadOnlyList<Item> Entities => holder.Current;

        public List<Contracts.Item> All()
        {
            return [.. Entities.Select(ItemMapper.ToContract)];
        }

        public Item? LookupItem(int itemId)
        {
            var items = Entities;
            return items.Count <= itemId || itemId < 0 ? null : items[itemId];
        }

        public CoreItem GetItem(int itemId)
        {
            return ItemMapper.ToCore(Entities[itemId]);
        }
    }
}
