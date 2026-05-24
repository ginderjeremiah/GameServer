using Game.Abstractions.Entities;

namespace Game.Abstractions.DataAccess
{
    public interface IZones
    {
        public void InvalidateCache();
        public List<Zone> All(bool refreshCache = false);
        public Zone? GetZone(int zoneId);
        public bool ValidateZoneId(int zoneId);
        public IAsyncEnumerable<ZoneEnemy> ZoneEnemies(int zoneId);
    }
}
