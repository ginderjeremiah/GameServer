using Contracts = Game.Abstractions.Contracts;
using CoreItemMod = Game.Core.Items.ItemMod;

namespace Game.Abstractions.DataAccess
{
    public interface IItemMods : ICacheInvalidatable
    {
        public List<Contracts.ItemMod> All(bool refreshCache = false);
        // Whether an item mod with the given id exists; lets callers validate before GetItemMod without touching the entity.
        public bool ValidateItemModId(int itemModId);
        public CoreItemMod GetItemMod(int itemModId);
    }
}
