using GameCore.Entities;

namespace GameCore.DataAccess
{
    public interface IZones
    {
        public Task<IEnumerable<Zone>> AllZonesAsync();
        public Task<Zone?> GetZoneAsync(int zoneId);
        public Task<bool> ValidateZoneIdAsync(int zoneId);
    }
}
