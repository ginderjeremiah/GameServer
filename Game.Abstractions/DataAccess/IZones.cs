using Contracts = Game.Abstractions.Contracts;

namespace Game.Abstractions.DataAccess
{
    public interface IZones
    {
        public void InvalidateCache();
        public List<Contracts.Zone> All(bool refreshCache = false);
        // Returns the read contract for a single zone (battle setup); throws if the id is out of range.
        public Contracts.Zone GetZone(int zoneId);
        public bool ValidateZoneId(int zoneId);
        public IAsyncEnumerable<Contracts.ZoneEnemy> ZoneEnemies(int zoneId);
    }
}
