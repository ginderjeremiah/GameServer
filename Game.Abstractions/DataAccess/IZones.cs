using Contracts = Game.Abstractions.Contracts;
using ZoneEntity = Game.Abstractions.Entities.Zone;

namespace Game.Abstractions.DataAccess
{
    public interface IZones
    {
        public void InvalidateCache();
        public List<Contracts.Zone> All(bool refreshCache = false);
        // Returns the read contract for a single zone (battle setup); throws if the id is out of range.
        public Contracts.Zone GetZone(int zoneId);
        // Returns the EF entity for the Content Authoring admin persistence (Game.DataAccess); the read path uses the contracts above. Internalized in #138.
        public ZoneEntity? LookupZone(int zoneId);
        public bool ValidateZoneId(int zoneId);
        public IAsyncEnumerable<Contracts.ZoneEnemy> ZoneEnemies(int zoneId);
    }
}
