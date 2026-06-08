using ZoneEntity = Game.Infrastructure.Entities.Zone;

namespace Game.DataAccess.Repositories
{
    /// <summary>
    /// Internal access to the cached zone <em>entities</em> for the Content Authoring admin persistence
    /// (<see cref="Repositories.Admin.AdminZones"/>), which needs the EF entity for existence/diff lookups.
    /// Kept out of the public <see cref="Abstractions.DataAccess.IZones"/> read contract — the entity is an
    /// implementation detail of this layer.
    /// </summary>
    internal interface IZoneEntityCache
    {
        /// <summary>The cached zone entity at <paramref name="zoneId"/> (its zero-based index), or null if out of range.</summary>
        ZoneEntity? LookupZone(int zoneId);
    }
}
