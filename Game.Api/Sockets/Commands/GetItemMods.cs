using Game.Abstractions.DataAccess;
using Game.Api.Models.Items;

namespace Game.Api.Sockets.Commands
{
    /// <summary>
    /// Returns the full item-modifier reference-data collection. WebSocket
    /// equivalent of the <c>GET /api/ItemMods</c> endpoint.
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
            return _itemMods.All().To().Model<ItemMod>();
        }
    }
}
