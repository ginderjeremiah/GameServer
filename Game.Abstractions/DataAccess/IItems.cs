using Contracts = Game.Abstractions.Contracts;
using ItemEntity = Game.Abstractions.Entities.Item;
using CoreItem = Game.Core.Items.Item;

namespace Game.Abstractions.DataAccess
{
    public interface IItems
    {
        public void InvalidateCache();
        public List<Contracts.Item> All(bool refreshCache = false);
        // Returns the EF entity for the admin Content Authoring write path (#135); the read path uses the contracts above.
        public ItemEntity? LookupItem(int itemId);
        public CoreItem GetItem(int itemId);
    }
}
