using Game.Abstractions.Contracts;
using Game.Abstractions.DataAccess;

namespace Game.Api.Sockets.Commands
{
    /// <summary>
    /// Serves the full zone reference-data set over the socket.
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

        protected override object VersionKey => _zones.VersionKey;
    }
}
