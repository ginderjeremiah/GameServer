using Game.Abstractions.DataAccess;
using Game.Infrastructure.Entities;
using Game.DataAccess.Mapping;
using Game.Infrastructure.Database;
using Contracts = Game.Abstractions.Contracts;

using Microsoft.EntityFrameworkCore;

namespace Game.DataAccess.Repositories
{
    internal class Zones : IZones, IZoneEntityCache
    {
        private static List<Zone>? _zoneList;

        private readonly GameContext _context;

        public Zones(GameContext context)
        {
            _context = context;
        }

        public void InvalidateCache() => _zoneList = null;

        private List<Zone> AllEntities(bool refreshCache = false)
        {
            if (_zoneList is null || refreshCache)
            {
                _zoneList = _context.Zones
                   .AsNoTracking()
                   .Include(z => z.ZoneEnemies)
                   .OrderBy(z => z.Id)
                   .ToList();
            }
            return _zoneList;
        }

        public List<Contracts.Zone> All(bool refreshCache = false)
        {
            return [.. AllEntities(refreshCache).Select(ZoneMapper.ToContract)];
        }

        public Contracts.Zone GetZone(int zoneId)
        {
            return ValidateZoneId(zoneId)
                ? ZoneMapper.ToContract(AllEntities()[zoneId])
                : throw new ArgumentOutOfRangeException(nameof(zoneId));
        }

        public Core.Zones.Zone GetDomainZone(int zoneId)
        {
            return ValidateZoneId(zoneId)
                ? ZoneMapper.ToCore(AllEntities()[zoneId])
                : throw new ArgumentOutOfRangeException(nameof(zoneId));
        }

        public Zone? LookupZone(int zoneId)
        {
            return ValidateZoneId(zoneId) ? AllEntities()[zoneId] : null;
        }

        public bool ValidateZoneId(int zoneId)
        {
            return zoneId >= 0 && zoneId < AllEntities().Count;
        }
    }
}
