using DataAccess.Models.Zones;

namespace DataAccess.Caches
{
    internal class ZoneCache : IZoneCache
    {
        private readonly List<Zone> _zoneList;

        public ZoneCache(IRepositoryManager repositoryManager)
        {
            _zoneList = repositoryManager.Zones.AllZones();
        }

        public List<Zone> AllZones()
        {
            return _zoneList;
        }

        public Zone GetZone(int zoneId)
        {
            if (!ValidateZoneId(zoneId))
            {
                throw new ArgumentOutOfRangeException(nameof(zoneId));
            }

            return _zoneList[zoneId];
        }

        public bool ValidateZoneId(int zoneId)
        {
            return zoneId >= 0 && zoneId < _zoneList.Count;
        }
    }

    public interface IZoneCache
    {
        public List<Zone> AllZones();
        public Zone GetZone(int zoneId);
        public bool ValidateZoneId(int zoneId);
    }
}
