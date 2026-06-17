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
        private IReadOnlyList<Item> Entities => holder.Current.Entities;

        // The snapshot instance changes on every build-then-swap, so it doubles as the content-version key.
        public object VersionKey => holder.Current;

        public List<Contracts.Item> All()
        {
            return [.. Entities.Select(ItemMapper.ToContract)];
        }

        public Item? LookupItem(int itemId)
        {
            return Entities.Lookup(itemId);
        }

        public CoreItem GetItem(int itemId)
        {
            // Returns the snapshot's shared, pre-materialized instance rather than rebuilding a fresh graph
            // per call. The model is reference data treated as immutable by every caller (the battle path
            // composes modifiers into a separate AttributeCollection; applied mods live on the player's
            // UnlockedItemSlot, never on the shared Item.ModSlots), so sharing is safe.
            return holder.Current.CoreItems.GetById(itemId, "item");
        }
    }
}
