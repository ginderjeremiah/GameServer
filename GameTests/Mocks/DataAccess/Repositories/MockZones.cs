using GameCore.DataAccess;
using GameCore.Entities.Zones;

namespace GameTests.Mocks.DataAccess.Repositories
{
    internal class MockZones : IZones
    {
        public List<Zone> Zones { get; set; } = new();
        public List<Zone> AllZones()
        {
            return Zones;
        }

        public Zone GetZone(int zoneId)
        {
            return Zones.First(zone => zone.ZoneId == zoneId);
        }

        public bool ValidateZoneId(int zoneId)
        {
            return Zones.Any(zone => zone.ZoneId == zoneId);
        }
    }
}
