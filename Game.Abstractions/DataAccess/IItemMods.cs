using Contracts = Game.Abstractions.Contracts;
using ItemModEntity = Game.Abstractions.Entities.ItemMod;
using CoreItemMod = Game.Core.Items.ItemMod;

namespace Game.Abstractions.DataAccess
{
    public interface IItemMods
    {
        public void InvalidateCache();
        public List<Contracts.ItemMod> All(bool refreshCache = false);
        // Returns the EF entity for the Content Authoring admin persistence (Game.DataAccess) and PlayerService (#137); the read path uses the contracts above.
        public ItemModEntity? LookupItemMod(int itemModId);
        public CoreItemMod GetItemMod(int itemModId);
    }
}
