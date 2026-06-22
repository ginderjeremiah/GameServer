using Game.Abstractions.DataAccess;
using Game.DataAccess.Mapping;
using Game.DataAccess.Repositories.Caching;
using Game.Infrastructure.Entities;
using Contracts = Game.Abstractions.Contracts;

namespace Game.DataAccess.Repositories
{
    internal class Zones(ZonesCacheHolder holder) : IZones, IZoneEntityCache
    {
        // A single volatile read of the current snapshot; capturing it once per call keeps the bounds check
        // and the index reading the same list, so a build-then-swap mid-call can never tear.
        private ZoneSnapshot Snapshot => holder.Current;

        // The snapshot instance changes on every build-then-swap, so it doubles as the content-version key.
        public object VersionKey => Snapshot;

        public List<Contracts.Zone> All()
        {
            return [.. Snapshot.Entities.Select(ZoneMapper.ToContract)];
        }

        public Contracts.Zone GetZone(int zoneId)
        {
            var entities = Snapshot.Entities;
            return IsValidZoneId(entities, zoneId)
                ? ZoneMapper.ToContract(entities[zoneId])
                : throw new ArgumentOutOfRangeException(nameof(zoneId));
        }

        public Core.Zones.Zone GetDomainZone(int zoneId)
        {
            // Hands back the snapshot's shared, pre-materialized domain model rather than mapping a fresh
            // CoreZone per call — this is the per-battle setup hot path and the model is immutable, so the
            // shared instance is safe (docs/backend.md → Reference Data).
            var snapshot = Snapshot;
            return IsValidZoneId(snapshot.Entities, zoneId)
                ? snapshot.CoreZones[zoneId]
                : throw new ArgumentOutOfRangeException(nameof(zoneId));
        }

        public Zone? LookupZone(int zoneId)
        {
            var entities = Snapshot.Entities;
            return IsValidZoneId(entities, zoneId) ? entities[zoneId] : null;
        }

        public bool IsZoneRetired(int zoneId)
        {
            var entities = Snapshot.Entities;
            return IsValidZoneId(entities, zoneId)
                ? entities[zoneId].RetiredAt is not null
                : throw new ArgumentOutOfRangeException(nameof(zoneId));
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
