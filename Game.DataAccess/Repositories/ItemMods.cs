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
        // Read the immutable snapshot once per logical operation (docs/backend.md → Reference-data snapshot
        // read-once idiom) so a build-then-swap between reads can't mix an old and a new snapshot in one call.
        private ItemModSnapshot Snapshot => holder.Current;

        // The snapshot instance changes on every build-then-swap, so it doubles as the content-version key.
        public object VersionKey => Snapshot;

        public List<Contracts.ItemMod> All()
        {
            return [.. Snapshot.Entities.Select(ItemMapper.ModToContract)];
        }

        public bool ValidateItemModId(int itemModId)
        {
            return itemModId >= 0 && itemModId < Snapshot.Entities.Count;
        }

        public ItemMod? LookupItemMod(int itemModId)
        {
            return Snapshot.Entities.Lookup(itemModId);
        }

        public CoreItemMod GetItemMod(int itemModId)
        {
            // Returns the snapshot's shared, pre-materialized instance rather than rebuilding a fresh graph
            // per call. Applied mods are reference data treated as immutable by every caller, so sharing is safe.
            return Snapshot.CoreItemMods.GetById(itemModId, "item mod");
        }
    }
}
