using Contracts = Game.Abstractions.Contracts;
using CoreItem = Game.Core.Items.Item;

namespace Game.Abstractions.DataAccess
{
    public interface IItems
    {
        public List<Contracts.Item> All();
        public CoreItem GetItem(int itemId);

        /// <summary>
        /// The current immutable snapshot instance, exposed only as an opaque identity token. Its reference
        /// changes on every build-then-swap, so callers can key a memo (e.g. the content-version hash) on it
        /// and have a cache swap invalidate the memo without exposing the cached entities.
        /// </summary>
        public object VersionKey { get; }
    }
}
