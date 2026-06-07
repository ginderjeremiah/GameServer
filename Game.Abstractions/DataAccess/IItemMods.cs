using Contracts = Game.Abstractions.Contracts;
using ItemModEntity = Game.Abstractions.Entities.ItemMod;
using CoreItemMod = Game.Core.Items.ItemMod;

namespace Game.Abstractions.DataAccess
{
    public interface IItemMods
    {
        public void InvalidateCache();
        public List<Contracts.ItemMod> All(bool refreshCache = false);
        // Whether an item mod with the given id exists; lets callers validate before GetItemMod without touching the entity.
        public bool ValidateItemModId(int itemModId);
        // Returns the EF entity for the Content Authoring admin persistence (Game.DataAccess); the read path uses the contracts above. Internalized in #138.
        public ItemModEntity? LookupItemMod(int itemModId);
        public CoreItemMod GetItemMod(int itemModId);
    }
}
