using Game.Abstractions.DataAccess;
using Game.Api.Models.Items;

namespace Game.Api.Sockets.Commands
{
    /// <summary>
    /// Returns the full item reference-data collection. WebSocket equivalent of
    /// the <c>GET /api/Items</c> endpoint.
    /// </summary>
    public class GetItems : AbstractReferenceDataCommand<Item>
    {
        private readonly IItems _items;

        public override string Name { get; set; } = nameof(GetItems);

        public GetItems(IItems items)
        {
            _items = items;
        }

        protected override IEnumerable<Item> GetReferenceData()
        {
            return _items.All().To().Model<Item>();
        }
    }
}
