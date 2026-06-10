using Contracts = Game.Abstractions.Contracts;
using CoreZone = Game.Core.Zones.Zone;

namespace Game.Abstractions.DataAccess
{
    public interface IZones : ICacheInvalidatable
    {
        public List<Contracts.Zone> All(bool refreshCache = false);
        // Returns the read contract for a single zone; throws if the id is out of range.
        public Contracts.Zone GetZone(int zoneId);

        /// <summary>Returns the lean gameplay <see cref="CoreZone"/> domain model for a single zone (battle
        /// setup); throws if the id is out of range.</summary>
        public CoreZone GetDomainZone(int zoneId);

        public bool ValidateZoneId(int zoneId);
        public IAsyncEnumerable<Contracts.ZoneEnemy> ZoneEnemies(int zoneId);
    }
}
