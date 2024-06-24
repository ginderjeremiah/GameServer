using GameCore.DataAccess;
using GameCore.Entities;
using GameCore.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace DataAccess.Repositories
{
    internal class Zones : BaseRepository, IZones
    {
        private static List<Zone>? _zoneList;

        public Zones(IDatabaseService database) : base(database) { }

        public async Task<IEnumerable<Zone>> AllZonesAsync()
        {
            return _zoneList ??= await Database.Zones
                .AsNoTracking()
                .Include(z => z.ZoneDrops)
                .ToListAsync();
        }

        public async Task<Zone?> GetZoneAsync(int zoneId)
        {
            if (!await ValidateZoneIdAsync(zoneId))
            {
                throw new ArgumentOutOfRangeException(nameof(zoneId));
            }

            return (await AllZonesAsync()).ToList()[zoneId];
        }

        public async Task<bool> ValidateZoneIdAsync(int zoneId)
        {
            return zoneId >= 0 && zoneId < (await AllZonesAsync()).ToList().Count;
        }
    }
}
