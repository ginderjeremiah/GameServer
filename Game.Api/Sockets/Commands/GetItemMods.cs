using Game.Abstractions.Contracts;
using Game.Abstractions.DataAccess;

namespace Game.Api.Sockets.Commands
{
    /// <summary>
    /// Serves the full item-modifier reference-data set over the socket.
    /// </summary>
    public class GetItemMods : AbstractReferenceDataCommand<ItemMod>
    {
        private readonly IItemMods _itemMods;

        public override string Name { get; set; } = nameof(GetItemMods);

        public GetItemMods(IItemMods itemMods)
        {
            _itemMods = itemMods;
        }

        protected override IEnumerable<ItemMod> GetReferenceData()
        {
            return _itemMods.All();
        }

        protected override object VersionKey => _itemMods.VersionKey;
    }
}
