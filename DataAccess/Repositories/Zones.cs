using GameCore.DataAccess;
using GameCore.Entities;
using GameInfrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace DataAccess.Repositories
{
    internal class Zones : BaseRepository, IZones
    {
        private static List<Zone>? _zoneList;

        public Zones(GameContext database) : base(database) { }

        public List<Zone> AllZones(bool refreshCache = false)
        {
            if (_zoneList is null || refreshCache)
            {
                _zoneList ??= [.. Database.Zones
                   .AsNoTracking()
                   .Include(z => z.ZoneDrops)];
            }
            return _zoneList;
        }

        public Zone? GetZone(int zoneId)
        {
            return !ValidateZoneId(zoneId)
                ? throw new ArgumentOutOfRangeException(nameof(zoneId))
                : AllZones()[zoneId];
        }

        public bool ValidateZoneId(int zoneId)
        {
            return zoneId >= 0 && zoneId < AllZones().Count;
        }
    }
}
