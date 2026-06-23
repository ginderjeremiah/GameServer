using Game.Abstractions.Contracts;
using Game.Abstractions.DataAccess;

namespace Game.Api.Sockets.Commands
{
    /// <summary>
    /// Serves the full item reference-data set over the socket.
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
            return _items.All();
        }

        protected override object VersionKey => _items.VersionKey;
    }
}
