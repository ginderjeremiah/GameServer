using Contracts = Game.Abstractions.Contracts;
using ZoneEntity = Game.Abstractions.Entities.Zone;

namespace Game.Abstractions.DataAccess
{
    public interface IZones
    {
        public void InvalidateCache();
        public List<Contracts.Zone> All(bool refreshCache = false);
        // Returns the EF entity for the Content Authoring admin persistence (Game.DataAccess) and battle setup (#137); the read path uses the contracts.
        public ZoneEntity? GetZone(int zoneId);
        public bool ValidateZoneId(int zoneId);
        public IAsyncEnumerable<Contracts.ZoneEnemy> ZoneEnemies(int zoneId);
    }
}
