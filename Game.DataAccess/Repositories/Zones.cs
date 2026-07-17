using Game.Abstractions.DataAccess;
using Game.DataAccess.Mapping;
using Game.DataAccess.Repositories.Caching;
using Game.Infrastructure.Entities;
using Contracts = Game.Abstractions.Contracts;

namespace Game.DataAccess.Repositories
{
    internal class Zones(ZonesCacheHolder holder) : IZones, IZoneEntityCache
    {
        // Read the immutable snapshot once per logical operation (docs/backend.md → Reference-data snapshot
        // read-once idiom) so a build-then-swap between reads can't mix an old and a new snapshot in one call.
        private ZoneSnapshot Snapshot => holder.Current;

        // The snapshot instance changes on every build-then-swap, so it doubles as the content-version key.
        public object VersionKey => Snapshot;

        public List<Contracts.Zone> All()
        {
            return [.. Snapshot.Entities.Select(ZoneMapper.ToContract)];
        }

        public Core.Zones.Zone GetDomainZone(int zoneId)
        {
            // Hands back the snapshot's shared, pre-materialized domain model rather than mapping a fresh
            // CoreZone per call — this is the per-battle setup hot path and the model is immutable, so the
            // shared instance is safe (docs/backend.md → Reference Data).
            return Snapshot.CoreZones.GetById(zoneId, "zone");
        }

        public Zone? LookupZone(int zoneId)
        {
            return Snapshot.Entities.Lookup(zoneId);
        }

        public IReadOnlyList<Zone> AllZones()
        {
            return Snapshot.Entities;
        }

        public bool IsZoneRetired(int zoneId)
        {
            return Snapshot.Entities.GetById(zoneId, "zone").RetiredAt is not null;
        }

        public bool IsHomeZone(int zoneId)
        {
            return Snapshot.Entities.GetById(zoneId, "zone").IsHome;
        }

        public bool ValidateZoneId(int zoneId)
        {
            return IsValidZoneId(Snapshot.Entities, zoneId);
        }

        private static bool IsValidZoneId(IReadOnlyList<Zone> entities, int zoneId)
        {
            return zoneId >= 0 && zoneId < entities.Count;
        }
    }
}
