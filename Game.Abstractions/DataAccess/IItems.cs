using Contracts = Game.Abstractions.Contracts;
using ItemEntity = Game.Abstractions.Entities.Item;
using CoreItem = Game.Core.Items.Item;

namespace Game.Abstractions.DataAccess
{
    public interface IItems
    {
        public void InvalidateCache();
        public List<Contracts.Item> All(bool refreshCache = false);
        // Returns the EF entity for the Content Authoring admin persistence (Game.DataAccess); the read path uses the contracts above.
        public ItemEntity? LookupItem(int itemId);
        public CoreItem GetItem(int itemId);
    }
}
