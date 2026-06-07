using Contracts = Game.Abstractions.Contracts;
using ItemModEntity = Game.Abstractions.Entities.ItemMod;
using CoreItemMod = Game.Core.Items.ItemMod;

namespace Game.Abstractions.DataAccess
{
    public interface IItemMods
    {
        public void InvalidateCache();
        public List<Contracts.ItemMod> All(bool refreshCache = false);
        // Returns the EF entity for the admin Content Authoring write path (#135); the read path uses the contracts above.
        public ItemModEntity? LookupItemMod(int itemModId);
        public CoreItemMod GetItemMod(int itemModId);
    }
}
