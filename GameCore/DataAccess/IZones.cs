using GameCore.Entities.Zones;

namespace GameCore.DataAccess
{
    public interface IZones
    {
        public List<Zone> AllZones();
        public Zone GetZone(int zoneId);
        public bool ValidateZoneId(int zoneId);
    }
}
