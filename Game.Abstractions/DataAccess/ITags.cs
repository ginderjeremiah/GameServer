using Contracts = Game.Abstractions.Contracts;

namespace Game.Abstractions.DataAccess
{
    public interface ITags
    {
        // Read contracts (the published reference-data read language).
        public IAsyncEnumerable<Contracts.Tag> All();
        public IAsyncEnumerable<Contracts.Tag> GetTagsForItem(int itemId);
        public IAsyncEnumerable<Contracts.Tag> GetTagsForItemMod(int itemModId);
    }
}
