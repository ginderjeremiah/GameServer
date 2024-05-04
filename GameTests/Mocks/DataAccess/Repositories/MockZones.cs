using DataAccess.Entities.Zones;
using DataAccess.Repositories;

namespace GameTests.Mocks.DataAccess.Repositories
{
    internal class MockZones : IZones
    {
        public List<Zone> AllZones()
        {
            throw new NotImplementedException();
        }

        public Zone GetZone(int zoneId)
        {
            throw new NotImplementedException();
        }

        public bool ValidateZoneId(int zoneId)
        {
            throw new NotImplementedException();
        }
    }
}
