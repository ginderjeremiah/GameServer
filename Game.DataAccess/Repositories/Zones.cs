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
            return ValidateZoneId(zoneId)
                ? ZoneMapper.ToContract(Entities[zoneId])
                : throw new ArgumentOutOfRangeException(nameof(zoneId));
        }

        public Core.Zones.Zone GetDomainZone(int zoneId)
        {
            return ValidateZoneId(zoneId)
                ? ZoneMapper.ToCore(Entities[zoneId])
                : throw new ArgumentOutOfRangeException(nameof(zoneId));
        }

        public Zone? LookupZone(int zoneId)
        {
            return ValidateZoneId(zoneId) ? Entities[zoneId] : null;
        }

        public bool ValidateZoneId(int zoneId)
        {
            return zoneId >= 0 && zoneId < Entities.Count;
        }
    }
}
