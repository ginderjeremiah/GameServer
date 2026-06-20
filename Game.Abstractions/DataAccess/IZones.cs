using Contracts = Game.Abstractions.Contracts;
using CoreZone = Game.Core.Zones.Zone;

namespace Game.Abstractions.DataAccess
{
    public interface IZones
    {
        public List<Contracts.Zone> All();
        // Returns the read contract for a single zone; throws if the id is out of range.
        public Contracts.Zone GetZone(int zoneId);

        /// <summary>Returns the lean gameplay <see cref="CoreZone"/> domain model for a single zone (battle
        /// setup); throws if the id is out of range.</summary>
        public CoreZone GetDomainZone(int zoneId);

        /// <summary>Whether the zone is retired (out of circulation). Reads the catalogue's retirement flag —
        /// retirement is a catalogue/authoring concern not carried on the lean gameplay <see cref="CoreZone"/>.
        /// Throws if the id is out of range.</summary>
        public bool IsZoneRetired(int zoneId);

        public bool ValidateZoneId(int zoneId);

        /// <inheritdoc cref="IItems.VersionKey"/>
        public object VersionKey { get; }
    }
}
