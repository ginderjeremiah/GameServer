using Game.Core.Entities;

namespace Game.Core.DataAccess
{
    public interface IZones
    {
        public List<Zone> All(bool refreshCache = false);
        public Zone? GetZone(int zoneId);
        public bool ValidateZoneId(int zoneId);
        public IAsyncEnumerable<ZoneEnemy> ZoneEnemies(int zoneId);
    }
}
