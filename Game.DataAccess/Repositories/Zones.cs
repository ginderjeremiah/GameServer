using Game.Core.DataAccess;
using Game.Core.Entities;
using Game.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace Game.DataAccess.Repositories
{
    internal class Zones : IZones
    {
        private static List<Zone>? _zoneList;

        private readonly GameContext _context;

        public Zones(GameContext context)
        {
            _context = context;
        }

        public List<Zone> All(bool refreshCache = false)
        {
            if (_zoneList is null || refreshCache)
            {
                _zoneList = _context.Zones
                   .AsNoTracking()
                   .Include(z => z.ZoneDrops)
                   .Include(z => z.ZoneEnemies)
                   .OrderBy(z => z.Id)
                   .ToList();
            }
            return _zoneList;
        }

        public Zone? GetZone(int zoneId)
        {
            return ValidateZoneId(zoneId)
                ? All()[zoneId]
                : throw new ArgumentOutOfRangeException(nameof(zoneId));
        }

        public bool ValidateZoneId(int zoneId)
        {
            return zoneId >= 0 && zoneId < All().Count;
        }

        public IAsyncEnumerable<ZoneEnemy> ZoneEnemies(int zoneId)
        {
            return _context.ZoneEnemies
                .Where(ze => ze.ZoneId == zoneId)
                .AsAsyncEnumerable();
        }
    }
}
