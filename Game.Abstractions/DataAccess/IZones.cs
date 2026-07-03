using Contracts = Game.Abstractions.Contracts;
using CoreZone = Game.Core.Zones.Zone;

namespace Game.Abstractions.DataAccess
{
    public interface IZones
    {
        public List<Contracts.Zone> All();

        /// <summary>Returns the lean gameplay <see cref="CoreZone"/> domain model for a single zone (battle
        /// setup); throws if the id is out of range.</summary>
        public CoreZone GetDomainZone(int zoneId);

        /// <summary>Whether the zone is retired (out of circulation). Reads the catalogue's retirement flag —
        /// retirement is a catalogue/authoring concern not carried on the lean gameplay <see cref="CoreZone"/>.
        /// Throws if the id is out of range.</summary>
        public bool IsZoneRetired(int zoneId);

        /// <summary>Whether the zone is the special no-combat <em>Home</em> sanctuary. Reads the catalogue's
        /// Home flag — like retirement, an authoring/orchestration concern not carried on the lean gameplay
        /// <see cref="CoreZone"/>. Throws if the id is out of range.</summary>
        public bool IsHomeZone(int zoneId);

        public bool ValidateZoneId(int zoneId);

        /// <inheritdoc cref="IItems.VersionKey"/>
        public object VersionKey { get; }
    }
}
