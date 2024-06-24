using GameCore.Entities;

namespace GameCore.DataAccess
{
    public interface IItems
    {
        public Task<IEnumerable<Item>> AllItemsAsync(bool refreshCache = false);
        public Task<Item?> GetItemAsync(int itemId);
    }
}
