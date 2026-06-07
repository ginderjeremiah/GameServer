using Game.Abstractions.Contracts;
using Game.Abstractions.DataAccess;

namespace Game.Api.Sockets.Commands
{
    /// <summary>
    /// Returns the full zone reference-data collection. WebSocket equivalent of
    /// the <c>GET /api/Zones</c> endpoint.
    /// </summary>
    public class GetZones : AbstractReferenceDataCommand<Zone>
    {
        private readonly IZones _zones;

        public override string Name { get; set; } = nameof(GetZones);

        public GetZones(IZones zones)
        {
            _zones = zones;
        }

        protected override IEnumerable<Zone> GetReferenceData()
        {
            return _zones.All();
        }
    }
}
