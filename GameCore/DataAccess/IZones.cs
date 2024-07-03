using GameCore.Entities;

namespace GameCore.DataAccess
{
    public interface IZones
    {
        public List<Zone> AllZones(bool refreshCache = false);
        public Zone? GetZone(int zoneId);
        public bool ValidateZoneId(int zoneId);
    }
}
