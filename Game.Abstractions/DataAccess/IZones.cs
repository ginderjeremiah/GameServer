using Game.Core.Zones;

namespace Game.Abstractions.DataAccess
{
    public interface IZones
    {
        public List<Zone> All(bool refreshCache = false);
        public Zone? GetZone(int zoneId);
        public bool ValidateZoneId(int zoneId);
    }
}
