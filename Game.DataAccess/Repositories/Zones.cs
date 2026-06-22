using Game.Abstractions.DataAccess;
using Game.DataAccess.Mapping;
using Game.DataAccess.Repositories.Caching;
using Game.Infrastructure.Entities;
using Contracts = Game.Abstractions.Contracts;

namespace Game.DataAccess.Repositories
{
    internal class Zones(ZonesCacheHolder holder) : IZones, IZoneEntityCache
    {
        private IReadOnlyList<Zone> Entities => holder.Current;

        // The snapshot instance changes on every build-then-swap, so it doubles as the content-version key.
        public object VersionKey => holder.Current;

        public List<Contracts.Zone> All()
        {
            return [.. Entities.Select(ZoneMapper.ToContract)];
        }

        public Contracts.Zone GetZone(int zoneId)
        {
            // Capture the volatile snapshot once so the bounds check and the index read the same list;
            // a build-then-swap publishing a shorter snapshot between the two reads would otherwise tear.
            var entities = holder.Current;
            return IsValidZoneId(entities, zoneId)
                ? ZoneMapper.ToContract(entities[zoneId])
                : throw new ArgumentOutOfRangeException(nameof(zoneId));
        }

        public Core.Zones.Zone GetDomainZone(int zoneId)
        {
            var entities = holder.Current;
            return IsValidZoneId(entities, zoneId)
                ? ZoneMapper.ToCore(entities[zoneId])
                : throw new ArgumentOutOfRangeException(nameof(zoneId));
        }

        public Zone? LookupZone(int zoneId)
        {
            var entities = holder.Current;
            return IsValidZoneId(entities, zoneId) ? entities[zoneId] : null;
        }

        public bool IsZoneRetired(int zoneId)
        {
            var entities = holder.Current;
            return IsValidZoneId(entities, zoneId)
                ? entities[zoneId].RetiredAt is not null
                : throw new ArgumentOutOfRangeException(nameof(zoneId));
        }

        public bool ValidateZoneId(int zoneId)
        {
            return IsValidZoneId(holder.Current, zoneId);
        }

        private static bool IsValidZoneId(IReadOnlyList<Zone> entities, int zoneId)
        {
            return zoneId >= 0 && zoneId < entities.Count;
        }
    }
}
